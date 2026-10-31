using UnityEngine;
using UnityEngine.EventSystems;

namespace KitchenDefense.Game
{
    /// <summary>보드 영역 탭을 받는 투명 판. UI 버튼이 위에 있으면 버튼이 먼저 받는다.</summary>
    public sealed class BoardInput : MonoBehaviour, IPointerClickHandler
    {
        public System.Action<Vector2> Tapped;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Tapped != null) Tapped(eventData.position);
        }
    }
}
