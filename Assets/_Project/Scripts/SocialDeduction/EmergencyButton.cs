using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class EmergencyButton : MonoBehaviour
    {
        private TextMesh label;

        private void Awake()
        {
            label = GetComponentInChildren<TextMesh>();

            if (label != null)
            {
                label.text = "紧急会议\nE";
            }
        }
    }
}
