using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using System.Collections;

namespace Localization
{
    public class LocaleSelector : MonoBehaviour
    {
        [SerializeField] private Button toggleLocaleButton;

        private void Start()
        {
            var savedLocaleIndex = PlayerPrefs.GetInt("localeIndex", 0);
            SetLocale(savedLocaleIndex);

            toggleLocaleButton.onClick.AddListener(ToggleLocale);
        }

        public void SetLocale(int localeIndex)
        {
            StartCoroutine(ChangeLocaleCoroutine(localeIndex));
        }

        private IEnumerator ChangeLocaleCoroutine(int localeIndex)
        {
            toggleLocaleButton.interactable = false;

            var selectedLocale = LocalizationSettings.AvailableLocales.Locales[localeIndex];
            var operation = LocalizationSettings.InitializationOperation;
            yield return operation;

            if (operation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                LocalizationSettings.SelectedLocale = selectedLocale;
                PlayerPrefs.SetInt("localeIndex", localeIndex);
            }
            else
            {
                Debug.LogError("Failed to initialize localization.");
            }

            toggleLocaleButton.interactable = true;
        }

        private void ToggleLocale()
        {
            var currentLocaleIndex = PlayerPrefs.GetInt("localeIndex", 0);
            var newLocaleIndex = (currentLocaleIndex + 1) % 2; // Assuming there are only 2 locales
            SetLocale(newLocaleIndex);
        }
    }
}  