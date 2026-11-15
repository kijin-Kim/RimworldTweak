using UnityEngine;
using UnityEngine.EventSystems;

namespace RimworldTweak.Presentation
{
    public sealed class BoardPointerHandler : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler
    {
        public SettlementView View { get; set; }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (View != null && View.TryScreenToGrid(eventData.position, out var position)) View.HandleMapHover(position);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (View == null || (eventData.button != PointerEventData.InputButton.Left && eventData.button != PointerEventData.InputButton.Right)) return;
            if (View.TryScreenToGrid(eventData.position, out var position))
                View.HandleMapClick(position, eventData.button == PointerEventData.InputButton.Right);
        }
    }
}
