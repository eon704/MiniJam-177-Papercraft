using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using System.Collections;

namespace Localization
{
    public class LocaleSelector : MonoBehaviour
    {
        private enum Locale
        {
            English,
            Russian,
            Kazakh,
            Chinese
        }

        [SerializeField] private Locale locale;
        private Button _button;

        private void Start()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(SetLocale);
        }

        private void SetLocale()
        {
            StartCoroutine(ChangeLocaleCoroutine());
        }

        private IEnumerator ChangeLocaleCoroutine()
        {
            _button.interactable = false;

            var localeIndex = locale switch
            {
                Locale.English => 0,
                Locale.Russian => 1,
                Locale.Kazakh => 2,
                Locale.Chinese => 3,
                _ => 0
            };

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

            _button.interactable = true;
        }
    }
}  