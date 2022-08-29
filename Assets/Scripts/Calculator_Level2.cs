using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Calculator_Level2 : MonoBehaviour
{
    private TMPro.TMP_Text Result;
    private int firstInput;
    private int secondinput;

    private TMPro.TMP_InputField FirstInputField;
    private TMPro.TMP_InputField SecondInputField;

    private TMPro.TMP_InputField CurrentInutField;


    //변수를 public 공개를 통해 유니티에서 할당하는 방식은 변경에 너무 취약할 수 밖에 없다. 
    //또한 여러사람이 작업 하기에 취약하다.
    public void Start()
    {
        TMPro.TMP_Text[] ArrTmpText = gameObject.GetComponentsInChildren<TMPro.TMP_Text>();

        for (int i = 0; i < ArrTmpText.Length; i++)
        {
            if (ArrTmpText[i].name == "Result")
            {
                Result = ArrTmpText[i];
            }
        }


        TMPro.TMP_InputField[] ArrTmpInput= gameObject.GetComponentsInChildren<TMPro.TMP_InputField>();

        for (int i = 0; i < ArrTmpInput.Length; i++)
        {
            if (ArrTmpInput[i].name == "Input1")
            {
                FirstInputField = ArrTmpInput[i];
                FirstInputField.onSelect.AddListener(delegate {OnSelect(FirstInputField);});
            }
            else if (ArrTmpInput[i].name == "Input2")
            {
                SecondInputField = ArrTmpInput[i];
                SecondInputField.onSelect.AddListener(delegate { OnSelect(SecondInputField); });
            }
        }

        UnityEngine.UI.Button[] ArrTmpButton= gameObject.GetComponentsInChildren<UnityEngine.UI.Button>();

        for (int i = 0; i < ArrTmpButton.Length; i++)
        {
            string btnName = ArrTmpButton[i].name;
            string[] nameSplt = btnName.Split('_');

            if(nameSplt.Length <=1) continue;

            if (nameSplt[0] != "Number" && nameSplt[0] != "Operator") continue;

            if (nameSplt[0] == "Number" && int.TryParse(nameSplt[1], out int result))
            {
                ArrTmpButton[i].onClick.AddListener(()=>SetNumber(result));
            }
            else if (nameSplt[0] == "Operator")
            {
                ArrTmpButton[i].onClick.AddListener(() => { Invoke(nameSplt[1],0);});
            }
        }
    }

    public void SetNumber(int number)
    {
        if (CurrentInutField != null)
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

    public void Multiple()
    {
        if (IsValid()) Result.text = (int.Parse(FirstInputField.text) * int.Parse(SecondInputField.text)).ToString();
    }
}
