using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MathGame.UI
{
    /// <summary>
    /// One visible blank slot in the question row.
    /// Displays a box outline when empty, fills with the chosen number.
    /// </summary>
    public class BlankSlot : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private Image           _background;

        [Header("Colors")]
        [SerializeField] private Color _emptyColor   = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color _filledColor  = new Color(0.2f, 0.6f, 1f, 0.8f);
        [SerializeField] private Color _correctColor = new Color(0.2f, 0.85f, 0.3f, 0.9f);
        [SerializeField] private Color _wrongColor   = new Color(0.9f, 0.2f, 0.2f, 0.9f);

        public bool IsFilled { get; private set; }
        public int  FilledValue { get; private set; }

        private void Awake() => SetEmpty();

        public void SetEmpty()
        {
            IsFilled       = false;
            FilledValue    = 0;
            _valueText.text = "?";
            _background.color = _emptyColor;
        }

        public void SetValue(int value)
        {
            IsFilled        = true;
            FilledValue     = value;
            _valueText.text = value.ToString();
            _background.color = _filledColor;
        }

        public void FlashCorrect() => StartCoroutine(FlashColor(_correctColor));
        public void FlashWrong()   => StartCoroutine(FlashColor(_wrongColor));

        private System.Collections.IEnumerator FlashColor(Color target)
        {
            _background.color = target;
            yield return new WaitForSeconds(0.8f);
            if (IsFilled)
                _background.color = _filledColor;
            else
                _background.color = _emptyColor;
        }
    }
}
