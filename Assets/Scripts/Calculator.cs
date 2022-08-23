using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Calculator : MonoBehaviour
{
    public TMPro.TMP_Text Result;
    public int firstInput;
    public int secondinput;

    public TMPro.TMP_InputField FirstInputField;
    public TMPro.TMP_InputField SecondInputField;

    TMPro.TMP_InputField CurrentInutField;

    public void SetNumber(int number)
    {
        if(CurrentInutField!=null)
        {
            CurrentInutField.text = number.ToString();
        }
    }

    public void OnSelect(TMPro.TMP_InputField inutField)
    {
        CurrentInutField = inutField;
    }

    public bool IsValid()
    {
        return (FirstInputField != null && SecondInputField != null &&
            string.IsNullOrWhiteSpace(FirstInputField.text) == false &&
            string.IsNullOrWhiteSpace(SecondInputField.text) == false);

    }

    public void Plus()
    {
        if (IsValid()) Result.text = (int.Parse(FirstInputField.text) + int.Parse(SecondInputField.text)).ToString();
    }

    public void Minus()
    {
        if (IsValid()) Result.text = (int.Parse(FirstInputField.text) - int.Parse(SecondInputField.text)).ToString();
    }

    public void Divide()
    {
        if (IsValid()) Result.text = (int.Parse(FirstInputField.text) / int.Parse(SecondInputField.text)).ToString();
    }

    public void DivideFloat()
    {
        if (IsValid()) Result.text = (float.Parse(FirstInputField.text) / float.Parse(SecondInputField.text)).ToString();
    }

    public void Mutiple()
    {
        if (IsValid()) Result.text = (int.Parse(FirstInputField.text) * int.Parse(SecondInputField.text)).ToString();
    }
}