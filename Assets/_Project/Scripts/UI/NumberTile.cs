using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MathGame.UI
{
    /// <summary>
    /// A single tappable number tile in the answer pool.
    /// When tapped it notifies the QuestionPanel and disables itself.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class NumberTile : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image           _background;

        [Header("Colors")]
        [SerializeField] private Color _activeColor   = new Color(0.15f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color _disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.4f);

        public int   Value      { get; private set; }
        public bool  IsUsed     { get; private set; }

        private Button              _button;
        private Action<NumberTile>  _onTapped;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        public void Setup(int value, Action<NumberTile> onTapped)
        {
            Value         = value;
            _onTapped     = onTapped;
            _label.text   = value.ToString();
            SetUsed(false);
        }

        public void SetUsed(bool used)
        {
            IsUsed             = used;
            _button.interactable = !used;
            _background.color  = used ? _disabledColor : _activeColor;
        }

        private void OnClick()
        {
            if (IsUsed) return;
            SetUsed(true);
            _onTapped?.Invoke(this);
        }
    }
}
