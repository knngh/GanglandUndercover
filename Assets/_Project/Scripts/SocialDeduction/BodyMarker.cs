using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class BodyMarker : MonoBehaviour
    {
        public SocialCharacter Victim { get; private set; }

        public void Bind(SocialCharacter victim)
        {
            Victim = victim;
        }
    }
}
