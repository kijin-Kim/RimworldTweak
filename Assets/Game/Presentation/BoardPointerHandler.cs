using UnityEngine;
using UnityEngine.EventSystems;

namespace RimworldTweak.Presentation
{
    public sealed class BoardPointerHandler : MonoBehaviour, IPointerClickHandler
    {
        public SettlementView View { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (View == null || (eventData.button != PointerEventData.InputButton.Left && eventData.button != PointerEventData.InputButton.Right)) return;
            if (View.TryScreenToGrid(eventData.position, out var position))
                View.HandleMapClick(position, eventData.button == PointerEventData.InputButton.Right);
        }
    }
}
