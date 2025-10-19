using UnityEngine;
using TMPro;

public class BuildingClickHandler : MonoBehaviour
{
    public TMP_Text infoText;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
            if (hit.collider != null)
            {
                BuildingInfo info = hit.collider.GetComponent<BuildingInfo>();
                if (info != null)
                {
                    infoText.text = $"Date: {info.builtDate}\nSpent On: {info.spentOn}\nSpent: £{info.spentAmount}";
                }
                else
                {
                    Debug.Log("no data for this");
                }
            }
            else
            {
                infoText.text = "";
            }
        }
    }
}