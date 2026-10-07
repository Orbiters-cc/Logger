using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// Small screenshots of the Scene view window for the undo timeline: what the window shows once it has drawn the
    /// step's result, read from its own framebuffer (no extra render, nothing on the desktop), scaled down on the GPU,
    /// read back without waiting and compressed to a small JPEG on a worker thread. Only while the Scene view is the
    /// visible tab of its dock. At most <see cref="MaxThumbnails"/> are kept (the oldest go first).
    /// </summary>
    internal static class SceneThumbnails
    {
        private const int Width = 240;
        private const int MaxThumbnails = 600;
        private const int Quality = 72;

        private static readonly object gate = new object();
        private static readonly Dictionary<int, byte[]> images = new Dictionary<int, byte[]>();
        private static readonly Queue<int> order = new Queue<int>();
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly FieldInfo Host = typeof(EditorWindow).GetField("m_Parent", Members);
        private static int nextId;
        private static UndoStep pending;
        private static bool repainted;
        private static bool started;

        public static void Start()
        {
            if (started)
            {
                return;
            }

            started = true;
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update += Update;
        }

        public static void Stop()
        {
            if (!started)
            {
                return;
            }

            started = false;
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= Update;
            pending = null;
            lock (gate)
            {
                images.Clear();
                order.Clear();
            }
        }

        /// <summary>Snapshots the Scene view for <paramref name="step"/> once it has drawn the step's result.</summary>
        public static void Capture(UndoStep step)
        {
            pending = step;
            repainted = false;
            SceneView.lastActiveSceneView?.Repaint();
        }

        /// <summary>The JPEG of a snapshot, or null.</summary>
        public static byte[] Get(int id)
        {
            lock (gate)
            {
                return id >= 0 && images.TryGetValue(id, out var jpg) ? jpg : null;
            }
        }

        /// <summary>Keeps a snapshot (read back after a script reload) and returns its id.</summary>
        public static int Add(byte[] jpg)
        {
            if (jpg == null || jpg.Length == 0)
            {
                return -1;
            }

            lock (gate)
            {
                int id = nextId++;
                images[id] = jpg;
                order.Enqueue(id);
                while (order.Count > MaxThumbnails)
                {
                    images.Remove(order.Dequeue());
                }

                return id;
            }
        }

        // The Scene view drew a frame after the step: its target texture now shows it.
        private static void OnSceneGui(SceneView view)
        {
            if (pending != null && Event.current != null && Event.current.type == EventType.Repaint && view == SceneView.lastActiveSceneView)
            {
                repainted = true;
            }
        }

        private static void Update()
        {
            if (pending == null || !repainted)
            {
                return;
            }

            var step = pending;
            pending = null;
            repainted = false;
            try
            {
                var view = SceneView.lastActiveSceneView;
                var host = view != null ? Host?.GetValue(view) : null;
                var shown = host?.GetType().GetProperty("actualView", Members)?.GetValue(host) as EditorWindow;
                var grab = host?.GetType().GetMethod("GrabPixels", Members, null, new[] { typeof(RenderTexture), typeof(Rect) }, null);
                if (shown != view || grab == null || !SystemInfo.supportsAsyncGPUReadback)
                {
                    return;
                }

                float scale = EditorGUIUtility.pixelsPerPoint;
                int fullWidth = Mathf.RoundToInt(view.position.width * scale);
                int fullHeight = Mathf.RoundToInt(view.position.height * scale);
                if (fullWidth <= 0 || fullHeight <= 0)
                {
                    return;
                }

                int width = Width;
                int height = Mathf.Clamp(Mathf.RoundToInt(Width * (float)fullHeight / fullWidth), 16, Width * 2);
                // Editor pixels are already display-encoded: linear targets keep them as they are.
                var full = RenderTexture.GetTemporary(fullWidth, fullHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var small = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var previous = RenderTexture.active;
                try
                {
                    grab.Invoke(host, new object[] { full, new Rect(0, 0, fullWidth, fullHeight) });
                    // The window's framebuffer is upside down for textures: flipped while scaling down.
                    Graphics.Blit(full, small, new Vector2(1f, -1f), new Vector2(0f, 1f));
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(full);
                }

                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, request =>
                {
                    try
                    {
                        if (request.hasError)
                        {
                            return;
                        }

                        byte[] pixels = request.GetData<byte>().ToArray();
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            try
                            {
                                byte[] jpg = ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0, Quality);
                                step.Thumbnail = Add(jpg);
                            }
                            catch (Exception)
                            {
                                // The step simply has no snapshot.
                            }
                        });
                    }
                    finally
                    {
                        RenderTexture.ReleaseTemporary(small);
                    }
                });
            }
            catch (Exception)
            {
                // No snapshot for this step.
            }
        }
    }
}
