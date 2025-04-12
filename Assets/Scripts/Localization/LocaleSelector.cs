using UnityEngine;
using UnityEngine.UI;

namespace Localization
{
    public class LocaleSelector : MonoBehaviour
    {
        [SerializeField] private LocalizationManager.Locale locale;
        private Button _button;

        private void Start()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(SetLocale);
        }

        private void SetLocale()
        {
            LocalizationManager.Instance.ChangeLocale(locale);
        }
    }
}  