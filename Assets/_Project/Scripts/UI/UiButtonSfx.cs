using GanglandUndercover.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    [RequireComponent(typeof(Selectable))]
    public sealed class UiButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private bool playHover = true;
        [SerializeField] private bool suppressWhenNotInteractable = true;

        public bool PlayHover => playHover;
        public bool SuppressWhenNotInteractable => suppressWhenNotInteractable;

        public void Configure(bool hover = true, bool suppressDisabled = true)
        {
            playHover = hover;
            suppressWhenNotInteractable = suppressDisabled;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PlayHoverCue();
        }

        public void OnSelect(BaseEventData eventData)
        {
            PlayHoverCue();
        }

        private void PlayHoverCue()
        {
            if (!playHover || !isActiveAndEnabled)
            {
                return;
            }

            if (suppressWhenNotInteractable
                && TryGetComponent<Selectable>(out Selectable selectable)
                && !selectable.interactable)
            {
                return;
            }

            AudioManager.Instance?.PlaySFX(SoundEffect.ButtonHover);
        }
    }
}
