using UnityEngine;
using UnityEngine.UI;

public class StarsUI : MonoBehaviour
{
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;
    
    public void OnStarChange(int newStar)
    {
        for (int i = 0; i < this.stars.Length; i++)
        {
            this.stars[i].sprite = i < newStar ? this.fullStar : this.emptyStar;
        }
    }

    private void Start()
    {
        this.OnStarChange(0);
    }
}