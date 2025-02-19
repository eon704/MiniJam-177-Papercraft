using UnityEngine;
using UnityEngine.UI;

public class StarsUI : MonoBehaviour
{
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;
    
    public void AddStar(int newStar)
    {
        this.stars[newStar].sprite = this.fullStar;
    }

    public void Reset()
    {
        foreach (Image star in this.stars)
        {
            star.sprite = this.emptyStar;
        }
    }

    private void Start()
    {
        this.Reset();
    }
}