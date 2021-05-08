
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDK3.Components.Video;

namespace ArchiTech
{
    public class SimplePlaylist : UdonSharpBehaviour
    {

        [HideInInspector] public RectTransform listContainer;
        [HideInInspector] public GameObject template;
        [HideInInspector] public TVManagerV2 tv;
        [HideInInspector] public bool autoplayList = false;
        [HideInInspector] public bool continueWhereLeftOff = true;
        [HideInInspector] public bool autoplayOnVideoError = true;
        [HideInInspector] public VRCUrl[] urls;
        [HideInInspector] public string[] titles;
        [HideInInspector] public bool showUrls;
        private byte nextAutoplayVideo = 0;
        private VideoError OUT_OnVideoPlayerError_VideoError_Error;

        private bool isLoading = false;
        private Slider loading;
        private float loadingBarDamp;
        private bool init = false;
        private bool skipLog = false;

        private void initialize()
        {
            if (init) return;
            if (titles.Length != urls.Length)
            {
                warn($"Titles count ({titles.Length}) doesn't match Urls count ({urls.Length}). Disregard this warning if that is intentional.");
            }
            if (tv == null) err("The TV reference was not provided. Please make sure the playlist knows what TV to connect to.");
            template.SetActive(false);
            if (autoplayList)
            {
                tv.autoplayURL = urls[nextAutoplayVideo];
                pickNext();
            }
            tv._RegisterUdonSharpEventReceiver(this);
            init = true;
        }

        void Start()
        {
            initialize();
        }

        void LateUpdate()
        {
            if (isLoading && loading != null)
            {
                if (loading.value > 0.95f) return;
                if (loading.value > 0.8f)
                    loading.value = Mathf.SmoothDamp(loading.value, 1f, ref loadingBarDamp, 0.4f);
                else
                    loading.value = Mathf.SmoothDamp(loading.value, 1f, ref loadingBarDamp, 0.3f);
            }
        }


        // === TV EVENTS ===

        public void _OnMediaEnd()
        {
            if (autoplayList && isTVOwner()) _SwitchTo(nextAutoplayVideo);
        }

        public void _OnMediaChange()
        {
            if (autoplayList && !continueWhereLeftOff && tv.localUrl.Get() != urls[nextAutoplayVideo].Get()) nextAutoplayVideo = 0;
        }

        public void _OnVideoPlayerError()
        {
            if (!autoplayOnVideoError || OUT_OnVideoPlayerError_VideoError_Error == VideoError.RateLimited) return; // TV auto-reloads on ratelimited, don't skip current video.
            if (autoplayList && tv.localUrl.Get() == urls[nextAutoplayVideo].Get())
            {
                pickNext();
                tv._QueueMedia(urls[nextAutoplayVideo]);
                pickNext();
            }
        }

        public void _OnLoading()
        {
            isLoading = true;
            if (loading != null) loading.value = 0f;

            var currentUrl = tv.localUrl.Get();
            for (int i = 0; i < urls.Length; i++)
            {
                if (urls[i].Get() == currentUrl)
                {
                    loading = listContainer.GetChild(i).GetComponentInChildren<Slider>();
                    break;
                }
            }
            if (loading != null) loading.value = 0f;
        }

        public void _OnLoadingEnd()
        {
            isLoading = false;
            if (loading != null) loading.value = 1f;
        }

        public void _OnLoadingStop()
        {
            isLoading = false;
            if (loading != null) loading.value = 0f;
        }


        // === UI EVENTS ===

        public void _Next()
        {
            increment();
            _SwitchTo(nextAutoplayVideo);
        }

        public void _Previous()
        {
            nextAutoplayVideo--;
            decrement();
            _SwitchTo(nextAutoplayVideo);
        }

        public void _SwitchTo(byte entry)
        {
            if (isLoading) return; // wait untill the current video loading finishes/fails
            if (entry >= urls.Length)
                err($"Playlist Item {entry} doesn't exist.");
            else
            {
                nextAutoplayVideo = entry;
                log($"Switching to playlist item {nextAutoplayVideo}");
                tv._ChangeMediaTo(urls[nextAutoplayVideo]);
                pickNext();
            }
        }

        private void pickNext()
        {
            var lastAutoplayVideo = nextAutoplayVideo;
            do
            {
                if (lastAutoplayVideo != nextAutoplayVideo)
                    log($"Item {nextAutoplayVideo} is missing, skipping");
                increment();
                if (nextAutoplayVideo == lastAutoplayVideo) break;
            } while (urls[nextAutoplayVideo].Get() == VRCUrl.Empty.Get());
            log($"Next playlist item {nextAutoplayVideo}");
        }

        private void increment()
        {
            nextAutoplayVideo++;
            if (nextAutoplayVideo >= urls.Length) nextAutoplayVideo = 0;
        }

        private void decrement()
        {
            nextAutoplayVideo--;
            if (nextAutoplayVideo < 0) nextAutoplayVideo = (byte)(urls.Length - 1);
        }

        private bool isTVOwner() => Networking.GetOwner(tv.gameObject) == Networking.LocalPlayer;

        public void _SwitchTo0() => _SwitchTo(0);
        public void _SwitchTo1() => _SwitchTo(1);
        public void _SwitchTo2() => _SwitchTo(2);
        public void _SwitchTo3() => _SwitchTo(3);
        public void _SwitchTo4() => _SwitchTo(4);
        public void _SwitchTo5() => _SwitchTo(5);
        public void _SwitchTo6() => _SwitchTo(6);
        public void _SwitchTo7() => _SwitchTo(7);
        public void _SwitchTo8() => _SwitchTo(8);
        public void _SwitchTo9() => _SwitchTo(9);
        public void _SwitchTo10() => _SwitchTo(10);
        public void _SwitchTo11() => _SwitchTo(11);
        public void _SwitchTo12() => _SwitchTo(12);
        public void _SwitchTo13() => _SwitchTo(13);
        public void _SwitchTo14() => _SwitchTo(14);
        public void _SwitchTo15() => _SwitchTo(15);
        public void _SwitchTo16() => _SwitchTo(16);
        public void _SwitchTo17() => _SwitchTo(17);
        public void _SwitchTo18() => _SwitchTo(18);
        public void _SwitchTo19() => _SwitchTo(19);
        public void _SwitchTo20() => _SwitchTo(20);
        public void _SwitchTo21() => _SwitchTo(21);
        public void _SwitchTo22() => _SwitchTo(22);
        public void _SwitchTo23() => _SwitchTo(23);
        public void _SwitchTo24() => _SwitchTo(24);
        public void _SwitchTo25() => _SwitchTo(25);
        public void _SwitchTo26() => _SwitchTo(26);
        public void _SwitchTo27() => _SwitchTo(27);
        public void _SwitchTo28() => _SwitchTo(28);
        public void _SwitchTo29() => _SwitchTo(29);
        public void _SwitchTo30() => _SwitchTo(30);
        public void _SwitchTo31() => _SwitchTo(31);
        public void _SwitchTo32() => _SwitchTo(32);
        public void _SwitchTo33() => _SwitchTo(33);
        public void _SwitchTo34() => _SwitchTo(34);
        public void _SwitchTo35() => _SwitchTo(35);
        public void _SwitchTo36() => _SwitchTo(36);
        public void _SwitchTo37() => _SwitchTo(37);
        public void _SwitchTo38() => _SwitchTo(38);
        public void _SwitchTo39() => _SwitchTo(39);
        public void _SwitchTo40() => _SwitchTo(40);
        public void _SwitchTo41() => _SwitchTo(41);
        public void _SwitchTo42() => _SwitchTo(42);
        public void _SwitchTo43() => _SwitchTo(43);
        public void _SwitchTo44() => _SwitchTo(44);
        public void _SwitchTo45() => _SwitchTo(45);
        public void _SwitchTo46() => _SwitchTo(46);
        public void _SwitchTo47() => _SwitchTo(47);
        public void _SwitchTo48() => _SwitchTo(48);
        public void _SwitchTo49() => _SwitchTo(49);
        public void _SwitchTo50() => _SwitchTo(50);
        public void _SwitchTo51() => _SwitchTo(51);
        public void _SwitchTo52() => _SwitchTo(52);
        public void _SwitchTo53() => _SwitchTo(53);
        public void _SwitchTo54() => _SwitchTo(54);
        public void _SwitchTo55() => _SwitchTo(55);
        public void _SwitchTo56() => _SwitchTo(56);
        public void _SwitchTo57() => _SwitchTo(57);
        public void _SwitchTo58() => _SwitchTo(58);
        public void _SwitchTo59() => _SwitchTo(59);
        public void _SwitchTo60() => _SwitchTo(60);
        public void _SwitchTo61() => _SwitchTo(61);
        public void _SwitchTo62() => _SwitchTo(62);
        public void _SwitchTo63() => _SwitchTo(63);
        public void _SwitchTo64() => _SwitchTo(64);
        public void _SwitchTo65() => _SwitchTo(65);
        public void _SwitchTo66() => _SwitchTo(66);
        public void _SwitchTo67() => _SwitchTo(67);
        public void _SwitchTo68() => _SwitchTo(68);
        public void _SwitchTo69() => _SwitchTo(69);
        public void _SwitchTo70() => _SwitchTo(70);
        public void _SwitchTo71() => _SwitchTo(71);
        public void _SwitchTo72() => _SwitchTo(72);
        public void _SwitchTo73() => _SwitchTo(73);
        public void _SwitchTo74() => _SwitchTo(74);
        public void _SwitchTo75() => _SwitchTo(75);
        public void _SwitchTo76() => _SwitchTo(76);
        public void _SwitchTo77() => _SwitchTo(77);
        public void _SwitchTo78() => _SwitchTo(78);
        public void _SwitchTo79() => _SwitchTo(79);
        public void _SwitchTo80() => _SwitchTo(80);
        public void _SwitchTo81() => _SwitchTo(81);
        public void _SwitchTo82() => _SwitchTo(82);
        public void _SwitchTo83() => _SwitchTo(83);
        public void _SwitchTo84() => _SwitchTo(84);
        public void _SwitchTo85() => _SwitchTo(85);
        public void _SwitchTo86() => _SwitchTo(86);
        public void _SwitchTo87() => _SwitchTo(87);
        public void _SwitchTo88() => _SwitchTo(88);
        public void _SwitchTo89() => _SwitchTo(89);
        public void _SwitchTo90() => _SwitchTo(90);
        public void _SwitchTo91() => _SwitchTo(91);
        public void _SwitchTo92() => _SwitchTo(92);
        public void _SwitchTo93() => _SwitchTo(93);
        public void _SwitchTo94() => _SwitchTo(94);
        public void _SwitchTo95() => _SwitchTo(95);
        public void _SwitchTo96() => _SwitchTo(96);
        public void _SwitchTo97() => _SwitchTo(97);
        public void _SwitchTo98() => _SwitchTo(98);
        public void _SwitchTo99() => _SwitchTo(99);

        private void log(string value)
        {
            if (!skipLog) Debug.Log($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#abbccd>SimplePlaylist</color>] {value}");
        }
        private void warn(string value)
        {
            if (!skipLog) Debug.LogWarning($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#abbccd>SimplePlaylist</color>] {value}");
        }
        private void err(string value)
        {
            if (!skipLog) Debug.LogError($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#abbccd>SimplePlaylist</color>] {value}");
        }
    }
}
