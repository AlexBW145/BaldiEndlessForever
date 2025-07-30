using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessFloorsForever.Components
{
    public class Inbox : MonoBehaviour, IClickable<int>
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image[] inventoryImage = [];
        [SerializeField] private Image[] inboxImage = [];
        private UpgradeSaveData[] InboxStuff = [];
        [SerializeField] private TMP_Text description;

        void Start()
        {
            description.color = Color.black;
            List<Image> slots = [];
            List<Image> inslots = [];
            for (int deslot = 0; deslot < 8; deslot++)
            {
                slots.Add(canvas.transform.Find("ItemSlots").Find(string.Format("ItemSlot ({0})", deslot)).GetComponentInChildren<Image>());
                inslots.Add(canvas.transform.Find("ItemImages").Find(string.Format("Image ({0})", deslot)).GetComponent<Image>());
            }
            inventoryImage = slots.ToArray();
            inboxImage = inslots.ToArray();
            for (int ful = 0; ful < 8; ful++)
            {
                if (ful < 6)
                {
                    inventoryImage[ful].transform.parent.gameObject.GetComponent<RawImage>().raycastTarget = true;
                    inventoryImage[ful].transform.parent.gameObject.tag = "Button";
                    inventoryImage[ful].transform.parent.gameObject.AddComponent<StandardMenuButton>();
                }
                inboxImage[ful].raycastTarget = true;
                inboxImage[ful].gameObject.tag = "Button";
                switch (ful)
                {
                    case 0:
                        inboxImage[ful].rectTransform.anchoredPosition = new(-120f, 6f);
                        break;
                    case 1:
                        inboxImage[ful].rectTransform.anchoredPosition = new(-40f, 6f);
                        break;
                    case 2:
                        inboxImage[ful].rectTransform.anchoredPosition = new(40f, 6f);
                        break;
                    case 3:
                        inboxImage[ful].rectTransform.anchoredPosition = new(120f, 6f);
                        break;
                    case 4:
                        inboxImage[ful].rectTransform.anchoredPosition = new(-120f, -63f);
                        break;
                    case 5:
                        inboxImage[ful].rectTransform.anchoredPosition = new(-40f, -63f);
                        break;
                    case 6:
                        inboxImage[ful].rectTransform.anchoredPosition = new(40f, -63f);
                        break;
                    case 7:
                        inboxImage[ful].rectTransform.anchoredPosition = new(120f, -63f);
                        break;
                }
                inboxImage[ful].gameObject.AddComponent<StandardMenuButton>();
                inboxImage[ful].gameObject.GetComponent<StandardMenuButton>().eventOnHigh = true;
            }
            InboxStuff = EndlessForeverPlugin.Instance.gameSave.inbox;
            canvas.transform.Find("Back").gameObject.GetComponent<StandardMenuButton>().OnPress = new UnityEngine.Events.UnityEvent();
            canvas.transform.Find("Back").gameObject.GetComponent<StandardMenuButton>().OnPress.AddListener(CloseUI);
            inventoryImage[0].transform.parent.parent.Find("Mask").gameObject.SetActive(false);
            if (!EndlessForeverPlugin.Instance.gameSave.IsInfGamemode)
                gameObject.SetActive(false);
            UpdateSlots();
            ChangeDesc("");
        }

        private void UpdateSlots()
        {
            InboxStuff = EndlessForeverPlugin.Instance.gameSave.inbox;
            for (int i = 0; i < inventoryImage.Count(); i++)
            {
                if (!inventoryImage[i].transform.parent.gameObject.activeSelf) continue;
                var but = inventoryImage[i].transform.parent.gameObject.GetComponent<StandardMenuButton>();
                but.eventOnHigh = true;
                but.OnPress = new UnityEngine.Events.UnityEvent();
                but.OnRelease = new UnityEngine.Events.UnityEvent();
                but.OnPress = new UnityEngine.Events.UnityEvent();
                but.OnHighlight = new UnityEngine.Events.UnityEvent();
                but.OffHighlight = new UnityEngine.Events.UnityEvent();
                int bugfix = i;
                but.OnPress.AddListener(() => ClickInventory(bugfix));
                UpgradeSaveData data = EndlessForeverPlugin.Instance.gameSave.Upgrades[i];
                StandardUpgrade upg = EndlessForeverPlugin.Upgrades[data.id];
                but.OnHighlight.AddListener(() => ChangeDesc(upg.GetLoca(data.count - 1), upg.id != "none"));
                but.OffHighlight.AddListener(() => ChangeDesc(""));
            }
            for (int j = 0; j < inboxImage.Count(); j++)
            {
                if (!inboxImage[j].gameObject.activeSelf) continue;
                StandardUpgrade upg = EndlessForeverPlugin.Upgrades[InboxStuff[j].id];
                int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(upg.id);
                var but = inboxImage[j].gameObject.GetComponent<StandardMenuButton>();
                but.OnPress = new UnityEngine.Events.UnityEvent();
                but.OnRelease = new UnityEngine.Events.UnityEvent();
                but.OnPress = new UnityEngine.Events.UnityEvent();
                but.OnHighlight = new UnityEngine.Events.UnityEvent();
                but.OffHighlight = new UnityEngine.Events.UnityEvent();
                int bugfix = j;
                but.OnPress.AddListener(() => ClickInbox(bugfix));
                but.OnHighlight.AddListener(() => ChangeDesc(upg.GetLoca(level), !EndlessForeverPlugin.Instance.gameSave.CanPurchaseUpgrade(upg, upg.behavior, EndlessForeverPlugin.Instance.gameSave.Upgrades.ToList().Exists(upgrade => upgrade.id == "none"))));
                but.OffHighlight.AddListener(() => ChangeDesc(""));
            }
            StorePatchHelpers.UpdateUpgradeBar(ref inventoryImage, CoreGameManager.Instance.NoneItem);
            for (int i = 0; i < inboxImage.Count(); i++)
            {
                if (!inboxImage[i].gameObject.activeSelf) continue;
                StandardUpgrade upg = EndlessForeverPlugin.Upgrades[InboxStuff[i].id];
                int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(upg.id);
                if (upg.id == "none")
                    inboxImage[i].sprite = CoreGameManager.Instance.NoneItem.itemSpriteSmall;
                else
                    inboxImage[i].sprite = upg.GetIcon(level);
            }
        }

        public bool ClickableHidden() => false;

        public bool ClickableRequiresNormalHeight() => false;

        public void ClickableSighted(int player)
        {
        }

        public void ClickableUnsighted(int player)
        {
        }

        public void Clicked(int player) => OpenUI();

        private void OpenUI()
        {
            if (GlobalCam.Instance.TransitionActive) return;
            CoreGameManager.Instance.disablePause = true;
            Time.timeScale = 0;
            InputManager.Instance.ActivateActionSet("Interface");
            canvas.gameObject.SetActive(true);
            GlobalCam.Instance.FadeIn(UiTransition.Dither, 0.01666667f);

            UpdateSlots();
        }

        internal void CloseUI()
        {
            GlobalCam.Instance.Transition(UiTransition.Dither, 0.01666667f);
            canvas.gameObject.SetActive(false);
            InputManager.Instance.ActivateActionSet("InGame");
            CoreGameManager.Instance.disablePause = false;
            Time.timeScale = 1;

            EndlessForeverPlugin.Instance.gameSave.inbox = InboxStuff;
        }

        private void ChangeDesc(string desc, bool isselling = false) => description.text = LocalizationManager.Instance.GetLocalizedText(desc) + (isselling ? LocalizationManager.Instance.GetLocalizedText("Upg_Remove") : "");
        
        private void ClickInventory(int index)
        {
            UpgradeSaveData data = EndlessForeverPlugin.Instance.gameSave.Upgrades[index];
            if (data.id == "none")
                return;
            else
            {
                StandardUpgrade myG = EndlessForeverPlugin.Upgrades[data.id];
                int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(myG.id);
                int sellPrice = myG.CalculateSellPrice(level);
                CoreGameManager.Instance.AddPoints(sellPrice, 0, true, false);
                EndlessForeverPlugin.Instance.gameSave.SellUpgrade(data.id);
                UpdateSlots();
                ChangeDesc("");
            }
        }

        private void ClickInbox(int index)
        {
            UpgradeSaveData data = InboxStuff[index];
            if (data.id == "none")
                return;
            else
            {
                StandardUpgrade myG = EndlessForeverPlugin.Upgrades[data.id];
                int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(myG.id);
                if (!EndlessForeverPlugin.Instance.gameSave.CanPurchaseUpgrade(myG, myG.behavior, false)) // Why do it the other way since it doesn't update on its own??
                {
                    int sellPrice = myG.CalculateSellPrice(level);
                    CoreGameManager.Instance.AddPoints(sellPrice, 0, true, false);
                    EndlessForeverPlugin.Instance.gameSave.SellUpgrade(data.id);
                }
                else
                    EndlessForeverPlugin.Instance.gameSave.PurchaseUpgrade(myG, myG.behavior, false);
                EndlessForeverPlugin.Instance.gameSave.RemoveFromInbox(data.id);
                UpdateSlots();
                ChangeDesc("");
            }
        }
    }
}
