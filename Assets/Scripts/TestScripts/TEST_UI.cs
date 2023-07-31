using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TEST_UI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText = default;

    void Start()
    {
        UpdateHealth(100);
    }

    void OnEnable()
    {
        PlayerController.OnDMG += UpdateHealth;
        PlayerController.OnHealing += UpdateHealth;
    }

    void OnDisable()
    {
        PlayerController.OnDMG -= UpdateHealth;
        PlayerController.OnHealing -= UpdateHealth;
    }

    private void UpdateHealth(float currHealth)
    {
        healthText.text = currHealth.ToString("00");
    }
}
