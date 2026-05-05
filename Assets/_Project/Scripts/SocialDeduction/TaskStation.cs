using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class TaskStation : MonoBehaviour
    {
        private MeshRenderer meshRenderer;
        private TextMesh label;
        private Material material;
        private int progress;

        public string TaskName { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsSabotaged { get; private set; }
        public int Progress => progress;
        public int RequiredProgress { get; private set; } = 3;

        public void Bind(string taskName)
        {
            TaskName = taskName;
            meshRenderer = GetComponent<MeshRenderer>();
            material = new Material(FindColorShader());
            meshRenderer.sharedMaterial = material;
            label = GetComponentInChildren<TextMesh>();
            RefreshVisual();
        }

        public void Complete()
        {
            progress = RequiredProgress;
            IsCompleted = true;
            IsSabotaged = false;
            RefreshVisual();
        }

        public void Work()
        {
            if (IsCompleted)
            {
                return;
            }

            IsSabotaged = false;
            progress++;

            if (progress >= RequiredProgress)
            {
                Complete();
                return;
            }

            RefreshVisual();
        }

        public void Sabotage()
        {
            IsCompleted = false;
            IsSabotaged = true;
            progress = Mathf.Max(0, progress - 1);
            RefreshVisual();
        }

        public void ResetTask()
        {
            IsCompleted = false;
            IsSabotaged = false;
            progress = 0;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (material != null)
            {
                if (IsCompleted)
                {
                    material.color = new Color(0.22f, 0.62f, 0.28f, 1f);
                }
                else if (IsSabotaged)
                {
                    material.color = new Color(0.72f, 0.22f, 0.14f, 1f);
                }
                else
                {
                    material.color = new Color(0.18f, 0.48f, 0.72f, 1f);
                }
            }

            if (label != null)
            {
                if (IsCompleted)
                {
                    label.text = TaskName + "\nOK";
                }
                else if (IsSabotaged)
                {
                    label.text = TaskName + "\n破坏 " + progress + "/" + RequiredProgress;
                }
                else
                {
                    label.text = TaskName + "\nE " + progress + "/" + RequiredProgress;
                }
            }
        }

        private static Shader FindColorShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDestroy()
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
