using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;

namespace NamelessGod
{
    internal sealed class FirstStagePointer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private FirstStageView _view;
        private RectTransform _rect;
        private UnityEngine.UI.Image _image;
        private Vector3 _originalPosition;
        private int _index;
        private bool _isCard;

        /// <summary>카드 또는 기도 인덱스와 화면 참조를 연결한다.</summary>
        internal void Configure(FirstStageView view, int index, bool isCard)
        {
            _view = view;
            _index = index;
            _isCard = isCard;
            _rect = GetComponent<RectTransform>();
            _image = GetComponent<UnityEngine.UI.Image>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_isCard || !_view.CanDragCard) return;
            _originalPosition = _rect.position;
            _image.raycastTarget = false;
            transform.SetAsLastSibling();
            _view.BeginCardDrag(_index);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isCard || _image.raycastTarget) return;
            _rect.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isCard || _image.raycastTarget) return;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            FirstStagePointer target = null;
            foreach (RaycastResult result in results)
            {
                var candidate = result.gameObject.GetComponentInParent<FirstStagePointer>();
                if (candidate != null && !candidate._isCard) { target = candidate; break; }
            }
            _rect.position = _originalPosition;
            _image.raycastTarget = true;
            if (target != null) _view.DropCard(_index, target._index);
            else _view.EndCardDrag();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isCard) _view.ShowPrayerTooltip(_index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isCard) _view.HidePrayerTooltip();
        }
    }
}
