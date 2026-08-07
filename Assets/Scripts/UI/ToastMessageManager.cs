using UnityEngine;

public class ToastMessageManager : MonoBehaviour
{
    [SerializeField] private ToastMessagePanel toastPrefab;

    public void Show(string message)
    {
        ToastMessagePanel toast =
            Instantiate(toastPrefab, transform);

        toast.Play(message);
    }
}