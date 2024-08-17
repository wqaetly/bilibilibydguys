using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Script.APITool;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Script
{
    public class GameEntry : Instance<GameEntry>
    {
        [FormerlySerializedAs("qrCode")] public Image img_QRCode;
        public Button btn_ScanedQRCode;

        /// <summary>
        /// 目标评论区Id
        /// </summary>
        public string oid = "1404984388";

        /// <summary>
        /// 评论区类型
        /// https://socialsisteryi.github.io/bilibili-API-collect/docs/comment/#%E8%AF%84%E8%AE%BA%E5%8C%BA%E7%B1%BB%E5%9E%8B%E4%BB%A3%E7%A0%81
        /// </summary>
        public string replyType = "1";

        public ScrollRect scrollView;
        public GameObject scrollContent;
        public GameObject scrollItem;

        public TMP_Text owner;

        private void Start()
        {
            Init().Forget();
        }

        public async UniTaskVoid Init()
        {
            this.scrollView.gameObject.SetActive(false);
            owner.transform.parent.gameObject.SetActive(false);
            img_QRCode.gameObject.SetActive(true);
            btn_ScanedQRCode.gameObject.SetActive(true);

            UniTaskCompletionSource uniTaskCompletionSource = new UniTaskCompletionSource();
            btn_ScanedQRCode.onClick.AddListener(() => { uniTaskCompletionSource.TrySetResult(); });

            // 如果登陆不成功，则重置流程
            if (!await BilibiliAPIManager.GetInstance.Login(uniTaskCompletionSource))
            {
                Init().Forget();
                return;
            }

            HideLoginUI();
            
            owner.transform.parent.gameObject.SetActive(true);
            this.scrollView.gameObject.SetActive(true);

            owner.text = $"中奖信息详情   执行UP主：<color=red>烟雨迷离半世殇</color>";
            ReplyTool.GetValidUser().Forget();
        }

        public void HideLoginUI()
        {
            btn_ScanedQRCode.onClick.RemoveAllListeners();

            img_QRCode.gameObject.SetActive(false);
            btn_ScanedQRCode.gameObject.SetActive(false);
        }

        public async UniTaskVoid AddItem(string info)
        {
            var go = GameObject.Instantiate(this.scrollItem, parent: this.scrollContent.transform).gameObject;

            var text = go.GetComponentInChildren<TMP_Text>(true);
            text.text = info;
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
            await UniTask.WaitForEndOfFrame();
            
            scrollView.verticalNormalizedPosition = 0;
        }
    }
}