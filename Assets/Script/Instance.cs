using UnityEngine;

namespace Script
{
    public class Instance<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T _instance;

        public static T GetInstance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindObjectOfType<T>();
                    if (_instance == null)
                    {
                        var go = new GameObject();
                        _instance = go.AddComponent<T>();
                    }
                }

                return _instance;
            }
        }
    }
}