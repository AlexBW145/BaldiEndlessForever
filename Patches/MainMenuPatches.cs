using EndlessFloorsForever.Components;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Reflection;
using MTM101BaldAPI.SaveSystem;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static System.Net.Mime.MediaTypeNames;

namespace EndlessFloorsForever.Patches
{
    [HarmonyPatch(typeof(MainMenu), "Start")]
    class MainMenuPatches
    {
        static void Postfix(ref GameObject ___seedInput)
        {
            var floor = GameObject.Instantiate(___seedInput, ___seedInput.transform.parent);
            floor.name = "FloorPicker";
            floor.transform.SetSiblingIndex(___seedInput.transform.GetSiblingIndex());
            floor.SetActive(false);
            floor.GetOrAddComponent<TextLocalizer>().ReflectionSetVariable("textBox", floor.GetComponent<TMP_Text>());
            floor.GetComponent<TextLocalizer>().key = "";
            floor.SetActive(true);
            GameObject.Destroy(floor.gameObject.GetComponent<SeedInput>());
            floor.AddComponent<FloorPick>();
            floor.GetComponent<StandardMenuButton>().OnPress = new UnityEvent();
            floor.GetComponent<StandardMenuButton>().OnPress.AddListener(() => floor.GetComponent<FloorPick>().StartSliding());

            //var thedupe = GameObject.Instantiate(RectTransform.FindObjectsOfType<RectTransform>(true).ToList().Find(x => x.name == "PickEndlessMap"));
            var theman = RectTransform.FindObjectsOfType<RectTransform>(true).First(x => x.name == "MainNew");
            theman.anchoredPosition = new(-120f, 112f);
            theman.GetComponent<StandardMenuButton>().OnPress.AddListener(() => EndlessForeverPlugin.Instance.gameSave.IsInfGamemode = false);
            //theman.parent.Find("Free").GetComponent<StandardMenuButton>().OnPress.AddListener(() => EndlessForeverPlugin.Instance.gameSave.IsInfGamemode = false);
            var theend = GameObject.Instantiate(theman, theman.parent);
            theend.gameObject.name = "MainEndlessFloors";
            theend.transform.SetSiblingIndex(theman.transform.GetSiblingIndex());
            theend.anchoredPosition = new(120f, 112f);
            var butt = theend.GetComponent<StandardMenuButton>();
            butt.OnPress = new UnityEvent();
            var warning = GameObject.FindObjectsOfType<GameObject>(true).First(x => x.name == "HideSeekWarning");
            UnityAction startnewinfinite = () => // Copy pasted from the cancelled Endless Floors to BCPP's debugging code to here... (Blame me for the stupidity)
            {
                var curF = floor.GetComponent<FloorPick>();
                EndlessForeverPlugin.Instance.gameSave.currentFloor = curF.floorNum;
                EndlessForeverPlugin.Instance.gameSave.startingFloor = curF.floorNum;
                InfGameManager.UpdateData();
                ModdedFileManager.Instance.DeleteSavedGame();
                GameLoader gl = GameObject.FindObjectsOfType<GameLoader>(true).First(x => x.gameObject.name == "GameLoader" && x.gameObject.scene.name == "MainMenu");
                gl.gameObject.SetActive(true);
                butt.transform.parent.gameObject.SetActive(false);
                warning.SetActive(false);
                gl.CheckSeed();
                gl.Initialize(2);
                gl.SetMode((int)Mode.Main);
                gl.ApplyGameSettingsForHideSeek();
                ElevatorScreen evl = SceneManager.GetActiveScene().GetRootGameObjects().Where(x => x.name == "ElevatorScreen").First().GetComponent<ElevatorScreen>();
                gl.AssignElevatorScreen(evl);
                evl.gameObject.SetActive(true);
                if (curF.floorNum > 4)
                {
                    gl.LoadLevel(EndlessForeverPlugin.Instance.InfPitstop);
                    CoreGameManager.Instance.nextLevel = EndlessForeverPlugin.Instance.inflevel;
                    CoreGameManager.Instance.AddPoints(FloorData.GetYTPsAtFloor(curF.floorNum), 0, false, false);
                }
                else
                    gl.LoadLevel(EndlessForeverPlugin.Instance.inflevel);
                if (curF.floorNum >= 6)
                {
                    for (int i = 6; i < curF.floorNum; i++)
                    {
                        if (EndlessForeverPlugin.Instance.gameSave.Counters["slots"] >= 9) break;
                        if ((i % (3 * Mathf.Max(1, Mathf.Min(EndlessForeverPlugin.Instance.gameSave.Counters["slots"], 9)))) == 0)
                            EndlessForeverPlugin.Instance.gameSave.Counters["slots"]++;
                    }
                }
                evl.Initialize();
                EndlessForeverPlugin.Instance.gameSave.IsInfGamemode = true;
                gl.SetSave(true);
            };
            butt.OnPress.AddListener(startnewinfinite);
            theend.GetComponent<TextLocalizer>().ReflectionSetVariable("textBox", theend.GetComponent<TMP_Text>());
            butt.OnHighlight = new UnityEvent();
            butt.OnHighlight.AddListener(() => theend.parent.Find("ModeText").GetComponent<TextLocalizer>().GetLocalizedText("Men_ENDDesc"));
            theend.gameObject.SetActive(false);
            theend.GetComponent<TextLocalizer>().key = "Men_END";
            theend.gameObject.SetActive(true);
            theend.GetComponent<TextLocalizer>().GetLocalizedText("Men_END");

            if (PlayerFileManager.Instance.clearedLevels[2] /*&& !(MTM101BaldiDevAPI.SaveGamesEnabled && ModdedFileManager.Instance.saveData.saveAvailable)*/)
            {
                ___seedInput.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
                ___seedInput.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
                ___seedInput.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
                ___seedInput.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 50f);
                ___seedInput.GetComponent<RectTransform>().anchoredPosition = new(100f, 0f);
                floor.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
                floor.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
                floor.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
                floor.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 50f);
                floor.GetComponent<RectTransform>().anchoredPosition = new(-100f, 0f);
            }

            warning.transform.Find("No").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -84f);
            warning.transform.Find("No").GetComponent<TextLocalizer>().key = "But_Back";
            warning.transform.Find("Yes").GetComponent<TextLocalizer>().key = "But_NewGame";
            warning.transform.Find("Yes").GetComponent<RectTransform>().sizeDelta += new Vector2(59f, 0f);
            warning.transform.Find("Yes").GetComponent<RectTransform>().anchoredPosition = new Vector2(82f, -32f);
            var endme = GameObject.Instantiate(warning.transform.Find("Yes"), warning.transform).GetComponent<RectTransform>();
            endme.anchoredPosition = new Vector2(-82f, -32f);
            endme.gameObject.name = "INFYes";
            endme.transform.SetSiblingIndex(warning.transform.Find("Yes").GetSiblingIndex());
            endme.GetComponent<StandardMenuButton>().OnPress = new UnityEvent();
            endme.GetComponent<StandardMenuButton>().OnPress.AddListener(startnewinfinite);
            endme.GetComponent<TextLocalizer>().key = "Men_END";
        }

        [HarmonyPatch(typeof(HideSeekMenu), "OnEnable"), HarmonyPostfix]
        static void CheckIfSaveAvailable(ref GameObject ___mainNew, ref GameObject ___mainContinue, ref GameObject ___mainNewWarning)
        {
            if (MTM101BaldiDevAPI.SaveGamesEnabled && ModdedFileManager.Instance.saveData.saveAvailable)
            {
                ___mainNew.transform.parent.Find("MainEndlessFloors").gameObject.SetActive(false);
                //___mainNew.transform.parent.Find("FloorPicker").gameObject.SetActive(false);
                if (EndlessForeverPlugin.Instance.gameSave.IsInfGamemode)
                {
                    ___mainContinue.GetComponent<StandardMenuButton>().OnPress.AddListener(InfGameManager.UpdateData);
                    /*___mainContinue.GetComponent<StandardMenuButton>().OnHighlight = new UnityEvent();
                    TextLocalizer local = ___mainContinue.transform.parent.Find("ModeText").GetComponent<TextLocalizer>();
                    ___mainContinue.GetComponent<StandardMenuButton>().OnHighlight.AddListener(() =>
                    {
                        local.GetLocalizedText("Men_ContENDDesc");
                        local.gameObject.GetComponent<TMP_Text>().text = string.Format(local.gameObject.GetComponent<TMP_Text>().text, EndlessForeverPlugin.Instance.gameSave.currentFloor, EndlessForeverPlugin.Instance.gameSave.startingFloor);
                    });*/

                    if ((EndlessForeverPlugin.Instance.gameSave.currentFloor + 1) - EndlessForeverPlugin.Instance.gameSave.startingFloor > 99)
                    {
                        var theman = ___mainNew.transform;
                        ___mainNewWarning.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110f, 80f);
                        ___mainNewWarning.GetComponent<RectTransform>().sizeDelta -= new Vector2(155f, 0f);
                        var newgame = GameObject.Instantiate(theman, theman.parent).GetComponent<StandardMenuButton>();
                        newgame.gameObject.name = "MainEndlessFloors_NewGamePlus";
                        newgame.transform.SetSiblingIndex(theman.GetSiblingIndex());
                        newgame.GetComponent<RectTransform>().anchoredPosition = new Vector2(110f, 80f);
                        newgame.GetComponent<TextLocalizer>().key = "But_NewGameINFPlus";
                        newgame.OnPress = new UnityEvent();
                        newgame.OnPress.AddListener(() =>
                        {
                            var prevupgrades = EndlessForeverPlugin.Instance.gameSave.Upgrades;
                            var previnbox = EndlessForeverPlugin.Instance.gameSave.inbox;
                            var prevcounters = EndlessForeverPlugin.Instance.gameSave.Counters;

                            EndlessForeverPlugin.Instance.gameSave.currentFloor = EndlessForeverPlugin.Instance.gameSave.startingFloor;
                            InfGameManager.UpdateData();

                            ModdedFileManager.Instance.DeleteSavedGame();
                            GameLoader gl = GameObject.FindObjectsOfType<GameLoader>(true).First(x => x.gameObject.name == "GameLoader" && x.gameObject.scene.name == "MainMenu");
                            gl.gameObject.SetActive(true);
                            newgame.transform.parent.gameObject.SetActive(false);
                            gl.CheckSeed();
                            int lives = 2;
                            if (prevcounters.ContainsKey("bonuslife"))
                                lives += Mathf.Min(prevcounters["bonuslife"], 3);
                            gl.Initialize(lives);
                            gl.SetMode((int)Mode.Main);
                            gl.ApplyGameSettingsForHideSeek();
                            ElevatorScreen evl = SceneManager.GetActiveScene().GetRootGameObjects().Where(x => x.name == "ElevatorScreen").First().GetComponent<ElevatorScreen>();
                            gl.AssignElevatorScreen(evl);
                            evl.gameObject.SetActive(true);
                            gl.LoadLevel(EndlessForeverPlugin.Instance.inflevel);
                            evl.Initialize();
                            EndlessForeverPlugin.Instance.gameSave.IsInfGamemode = true;
                            gl.SetSave(true);

                            EndlessForeverPlugin.Instance.gameSave.Upgrades = prevupgrades;
                            EndlessForeverPlugin.Instance.gameSave.inbox = previnbox;
                            EndlessForeverPlugin.Instance.gameSave.Counters = prevcounters;

                        });
                        newgame.OnHighlight = new UnityEvent();
                        newgame.OnHighlight.AddListener(() => theman.parent.GetComponent<TooltipController>().UpdateTooltip("Men_NewGameINFPlusDesc"));
                        newgame.OffHighlight = new UnityEvent();
                        newgame.OffHighlight.AddListener(theman.parent.GetComponent<TooltipController>().CloseTooltip);
                        newgame.eventOnHigh = true;
                        newgame.GetComponent<RectTransform>().sizeDelta -= new Vector2(140f, 0f);
                        newgame.gameObject.SetActive(true);
                    }
                }
            }
        }
    }
}
