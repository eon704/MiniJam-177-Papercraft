using UnityEngine;
using UnityEngine.UI;

public class StarsUI : MonoBehaviour
{
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;
    
    public void OnStarChange(int newStar)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].sprite = i < newStar ? fullStar : emptyStar;
        }
        
    }

    private void Start()
    {
        OnStarChange(0);
    }
}