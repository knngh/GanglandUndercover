using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class TaskStation : MonoBehaviour
    {
        private MeshRenderer meshRenderer;
        private TextMesh label;

        public string TaskName { get; private set; }
        public bool IsCompleted { get; private set; }

        public void Bind(string taskName)
        {
            TaskName = taskName;
            meshRenderer = GetComponent<MeshRenderer>();
            label = GetComponentInChildren<TextMesh>();
            RefreshVisual();
        }

        public void Complete()
        {
            IsCompleted = true;
            RefreshVisual();
        }

        public void ResetTask()
        {
            IsCompleted = false;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = IsCompleted
                    ? new Color(0.22f, 0.62f, 0.28f, 1f)
                    : new Color(0.18f, 0.48f, 0.72f, 1f);
            }

            if (label != null)
            {
                label.text = IsCompleted ? TaskName + "\nOK" : TaskName + "\nE";
            }
        }
    }
}
