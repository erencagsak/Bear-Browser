using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class WebViewService
    {
        private static WebViewService? _instance;
        public static WebViewService Instance => _instance ??= new WebViewService();

        private CoreWebView2Environment? _environment;
        private readonly string _userDataFolder;

        public WebViewService()
        {
            _userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser",
                "Profile"
            );
        }

        public async Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environment != null)
                return _environment;

            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true,
                AdditionalBrowserArguments =
                    "--disable-blink-features=AutomationControlled " +
                    "--enable-quic " +
                    "--enable-async-dns " +
                    "--enable-tcp-fast-open " +
                    "--enable-gpu-rasterization " +
                    "--enable-zero-copy " +
                    "--use-angle=d3d11 " +
                    "--enable-accelerated-2d-canvas " +
                    "--process-per-site " +
                    "--renderer-process-limit=4 " +
                    "--disable-features=IsolateOrigins,site-per-process,Translate,OptimizationHints,MediaRouter " +
                    "--enable-features=TurnOffStreamingMediaWithHiddenWindow,BackForwardCache,AutomaticTabDiscarding,WebContentsDiscard,CalculateNativeWinOcclusion,UseEcoQoSForBackgroundProcess,UseEcoQoSForRenderers " +
                    "--disk-cache-size=104857600 " +
                    "--media-cache-size=52428800 " +
                    "--js-flags=\"--max_old_space_size=256 --optimize_for_size --expose-gc\" " +
                    "--high-dpi-support=1"
            };

            _environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataFolder,
                options: options
            );

            return _environment;
        }

        public async Task InitializeWebViewAsync(WebView2 webView, bool isInPrivate = false)
        {
            CoreWebView2Environment env;

            if (isInPrivate)
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), $"Bear_InPrivate_{Guid.NewGuid()}");
                var options = new CoreWebView2EnvironmentOptions
                {
                    AreBrowserExtensionsEnabled = true,
                    AdditionalBrowserArguments =
                        "--disable-blink-features=AutomationControlled " +
                        "--enable-gpu-rasterization --enable-zero-copy --use-angle=d3d11 " +
                        "--process-per-site --renderer-process-limit=3 " +
                        "--disable-features=IsolateOrigins,site-per-process " +
                        "--enable-features=TurnOffStreamingMediaWithHiddenWindow,CalculateNativeWinOcclusion,UseEcoQoSForBackgroundProcess,UseEcoQoSForRenderers " +
                        "--js-flags=\"--max_old_space_size=256 --optimize_for_size\""
                };
                env = await CoreWebView2Environment.CreateWithOptionsAsync(null, tempFolder, options);
            }
            else
            {
                env = await GetEnvironmentAsync();
            }

            await webView.EnsureCoreWebView2Async(env);

            if (webView.CoreWebView2 != null && !isInPrivate)
            {
                _ = ExtensionService.Instance.SyncInstalledExtensionsAsync(webView.CoreWebView2.Profile);
            }

            if (webView.CoreWebView2 != null)
            {
                var settings = webView.CoreWebView2.Settings;
                settings.IsStatusBarEnabled = false;
                settings.AreDevToolsEnabled = true;
                settings.IsBuiltInErrorPageEnabled = false;
                settings.IsGeneralAutofillEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;

                // ⚡ KRİTİK AYAR: Chromium'un Ctrl+T / Ctrl+W tuşlarını yutmasını engelliyoruz!
                settings.AreBrowserAcceleratorKeysEnabled = false;

                // WebView2 Motoru veya Render İşlemi Çökmesi Yakalayıcısı
                webView.CoreWebView2.ProcessFailed += (s, args) =>
                {
                    string info = $"İşlem Türü: {args.ProcessFailedKind}\nNeden: {args.Reason}\nÇıkış Kodu: {args.ExitCode}\nAçıklama: {args.ProcessDescription}";
                    CrashReportingService.Instance.LogCustomError("WebView2 İşlem Hatası (ProcessFailed)", info);
                };

                // Arama kutusunda bile olsan tuşu yakalayan en üst düzey donanımsal kanca
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    (() => {
                        const nativeBridge = window.chrome?.webview;
                        const sendToNative = (msg) => {
                            try {
                                if (nativeBridge && nativeBridge.postMessage) {
                                    nativeBridge.postMessage(msg);
                                }
                            } catch(e) {}
                        };
                        window.addEventListener('keydown', (e) => {
                            // F11 : Tam Ekran Aç / Kapat
                            if (e.key === 'F11') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:ToggleFullScreen');
                                return;
                            }
                            // F12 : Geliştirici Araçları
                            if (e.key === 'F12') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:DevTools');
                                return;
                            }
                            // Escape : Tam Ekrandan Çık
                            if (e.key === 'Escape') {
                                sendToNative('shortcut:ExitFullScreen');
                                return;
                            }

                            if (!e.ctrlKey) return;

                            // Ctrl + T (Yeni sekme)
                            if (!e.shiftKey && e.code === 'KeyT') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:NewTab');
                            }
                            // Ctrl + N (Yeni pencere)
                            else if (!e.shiftKey && e.code === 'KeyN') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:NewWindow');
                            }
                            // Ctrl + Shift + N (Yeni InPrivate)
                            else if (e.shiftKey && e.code === 'KeyN') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:NewInPrivate');
                            }
                            // Ctrl + W (Sekmeyi kapat)
                            else if (!e.shiftKey && e.code === 'KeyW') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:CloseTab');
                            }
                            // Ctrl + H (Geçmiş)
                            else if (!e.shiftKey && e.code === 'KeyH') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:History');
                            }
                            // Ctrl + F (Sayfada Bul)
                            else if (!e.shiftKey && e.code === 'KeyF') {
                                e.preventDefault();
                                e.stopPropagation();
                                sendToNative('shortcut:FindInPage');
                            }
                        }, true);

                                                // --- GELİŞMİŞ PAROLA VE GİRİŞ YAKALAYICI (Akıllı Ayrım & Güvenlik Korumalı) ---
                        let lastUsername = '';
                        let lastPassword = '';

                        document.addEventListener('input', (e) => {
                            try {
                                if (!e.target) return;
                                const t = e.target;
                                const id = t.id?.toLowerCase() || '';
                                const name = t.name?.toLowerCase() || '';
                                const auto = t.autocomplete?.toLowerCase() || '';
                                const type = t.type?.toLowerCase() || '';

                                // ⚡ 1. ŞİFRE ALANI KONTROLÜ (Şifre alanları KESİNLİKLE kullanıcı adı olamaz!)
                                const isPass = type === 'password' || name.includes('pass') || id.includes('pass') || auto.includes('password');
                                if (isPass) {
                                    if (t.value && t.value.length > 0) lastPassword = t.value;
                                    return; // Şifre girilirken kullanıcı adını ASLA ezme, doğrudan çık!
                                }

                                // ⚡ 2. KULLANICI ADI ALANI KONTROLÜ
                                const isUser = type === 'email' || auto.includes('username') || auto.includes('email') ||
                                               id === 'username' || id === 'identifierid' || id.includes('user') || id.includes('login') ||
                                               name.includes('user') || name.includes('email') || name.includes('login') ||
                                               (type === 'text' && !name.includes('pass') && !id.includes('pass'));

                                if (isUser && t.value && t.value.trim().length > 0) {
                                    lastUsername = t.value.trim();
                                }
                            } catch(err) {}
                        }, true);

                        function notifyPassword() {
                            try {
                                const passInputs = Array.from(document.querySelectorAll('input[type=""password""], input[name*=""pass"" i]'));
                                const activePass = passInputs.find(p => p.value && p.value.length > 0);
                                const pass = activePass ? activePass.value : lastPassword;
                                if (!pass || pass.length === 0) return;

                                let uName = lastUsername;

                                // Eğer uName şifreye eşitse veya boşsa, DOM'daki gerçek kullanıcı adını filtreleyerek bul:
                                if (!uName || uName === pass) {
                                    const allInputs = Array.from(document.querySelectorAll('input:not([type=""password""]):not([type=""hidden""])'));
                                    const userInput = allInputs.find(input => {
                                        const id = input.id?.toLowerCase() || '';
                                        const name = input.name?.toLowerCase() || '';
                                        const auto = input.autocomplete?.toLowerCase() || '';
                                        const type = input.type?.toLowerCase() || '';

                                        if (name.includes('pass') || id.includes('pass') || auto.includes('pass')) return false;
                                        if (input.value === pass) return false;

                                        return type === 'email' || auto.includes('username') || auto.includes('email') ||
                                               id === 'username' || id === 'identifierid' || id.includes('user') ||
                                               name.includes('user') || name.includes('email') || id.includes('login');
                                    });

                                    if (userInput && userInput.value && userInput.value.trim().length > 0 && userInput.value.trim() !== pass) {
                                        uName = userInput.value.trim();
                                    }
                                }

                                // Gmail veya servis profil rozeti kontrolü
                                if (!uName || uName === pass) {
                                    const badge = document.querySelector('[data-profile-identifier], #profileIdentifier, [data-email]');
                                    if (badge) {
                                        const badgeEmail = badge.getAttribute('data-email') || badge.getAttribute('data-profile-identifier') || badge.textContent?.trim() || '';
                                        if (badgeEmail && badgeEmail !== pass) uName = badgeEmail;
                                    }
                                }

                                // 🔒 Son Güvenlik Kilidi: Kullanıcı adı şifreye eşitse kesinlikle kullanıcı adını boş bırak!
                                if (uName === pass) {
                                    uName = '';
                                }

                                sendToNative('password_submit:' + window.location.href + '|||' + (uName || '') + '|||' + pass);
                            } catch(err) {}
                        }

                        // ⚡ Form gönderme, Enter tuşu ve tıklama dinleyicileri:
                        document.addEventListener('submit', () => notifyPassword(), true);

                        document.addEventListener('keydown', (e) => {
                            if (e.key === 'Enter') {
                                notifyPassword();
                            }
                        }, true);

                        document.addEventListener('click', (e) => {
                            try {
                                if (e.target && e.target.closest('button, input[type=""submit""], [role=""button""], #passwordNext, [id*=""Next""], [id*=""login""], [id*=""submit""], [type=""button""]')) {
                                    notifyPassword();
                                }
                            } catch(err) {}
                        }, true);

                        // --- ETKİLEŞİMLİ PAROLA SEÇİM POPUP'I (AUTOFILL DROPDOWN) ---
                        window.__bearAccounts = [];
                        let currentAutofillInput = null;
                        let isSelectingAccount = false;
                        let justDismissed = false;

                        const getOrCreateAutofillPopup = () => {
                            let p = document.getElementById('bear-autofill-popup');
                            if (!p) {
                                p = document.createElement('div');
                                p.id = 'bear-autofill-popup';
                                p.style.setProperty('position', 'fixed', 'important');
                                p.style.setProperty('z-index', '2147483647', 'important');
                                p.style.setProperty('background', '#ffffff', 'important');
                                p.style.setProperty('color', '#1c1c1e', 'important');
                                p.style.setProperty('border', '1px solid #d1d1d6', 'important');
                                p.style.setProperty('border-radius', '10px', 'important');
                                p.style.setProperty('box-shadow', '0 10px 30px rgba(0, 0, 0, 0.15), 0 2px 8px rgba(0,0,0,0.06)', 'important');
                                p.style.setProperty('font-family', '-apple-system, BlinkMacSystemFont, Segoe UI, Roboto, Helvetica, Arial, sans-serif', 'important');
                                p.style.setProperty('font-size', '13px', 'important');
                                p.style.setProperty('min-width', '250px', 'important');
                                p.style.setProperty('max-width', '380px', 'important');
                                p.style.setProperty('padding', '6px 0', 'important');
                                p.style.setProperty('display', 'none', 'important');
                                p.style.setProperty('user-select', 'none', 'important');
                                p.style.setProperty('box-sizing', 'border-box', 'important');

                                p.addEventListener('mousedown', (e) => e.stopPropagation());
                                p.addEventListener('mouseup', (e) => e.stopPropagation());
                                p.addEventListener('click', (e) => e.stopPropagation());
                            }
                            const targetParent = document.body || document.documentElement;
                            if (targetParent && p.parentElement !== targetParent) {
                                targetParent.appendChild(p);
                            }
                            return p;
                        };

                        const hideAutofillPopup = () => {
                            const p = document.getElementById('bear-autofill-popup');
                            if (p) p.style.setProperty('display', 'none', 'important');
                            currentAutofillInput = null;
                        };

                        const fillAutofillAccount = (acc) => {
                            if (!acc) return;
                            const setVal = (el, val) => {
                                if (!el || !val) return;
                                try {
                                    const desc = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
                                    if (desc && desc.set) {
                                        desc.set.call(el, val);
                                    } else {
                                        el.value = val;
                                    }
                                } catch(e) { el.value = val; }
                                el.dispatchEvent(new Event('input', { bubbles: true }));
                                el.dispatchEvent(new Event('change', { bubbles: true }));
                            };

                            const allInputs = Array.from(document.querySelectorAll('input:not([type=""hidden""]):not([type=""submit""]):not([type=""button""]):not([type=""checkbox""]):not([type=""radio""])'));
                            const userInputs = allInputs.filter(i => i.type !== 'password');
                            const userInput = (currentAutofillInput && currentAutofillInput.type !== 'password' ? currentAutofillInput : null) ||
                                userInputs.find(i => {
                                    const id = (i.id || '').toLowerCase();
                                    const name = (i.name || '').toLowerCase();
                                    const auto = (i.autocomplete || '').toLowerCase();
                                    const type = (i.type || '').toLowerCase();
                                    return type === 'email' || auto.includes('username') || auto.includes('email') ||
                                           id.includes('user') || id.includes('email') || id.includes('login') || id.includes('identifier') ||
                                           name.includes('user') || name.includes('email') || name.includes('login') || name.includes('identifier');
                                }) || userInputs[0];

                            if (userInput && acc.user) {
                                setVal(userInput, acc.user);
                            }

                            const passInputs = allInputs.filter(i => i.type === 'password');
                            const passInput = (currentAutofillInput && currentAutofillInput.type === 'password' ? currentAutofillInput : null) ||
                                              passInputs[0] ||
                                              document.querySelector('input[name*=""pass"" i], input[id*=""pass"" i]');

                            if (passInput && acc.pass) {
                                setVal(passInput, acc.pass);
                            }

                            hideAutofillPopup();
                        };

                        const showAutofillPopup = (target) => {
                            if (!target || isSelectingAccount) return;
                            currentAutofillInput = target;
                            const accounts = window.__bearAccounts || [];
                            if (accounts.length === 0) {
                                sendToNative('request_autofill:' + window.location.href);
                                return;
                            }

                            const p = getOrCreateAutofillPopup();
                            while (p.firstChild) {
                                p.removeChild(p.firstChild);
                            }

                            const header = document.createElement('div');
                            header.style.setProperty('padding', '6px 14px 8px 14px', 'important');
                            header.style.setProperty('font-size', '11px', 'important');
                            header.style.setProperty('font-weight', '600', 'important');
                            header.style.setProperty('color', '#8e8e93', 'important');
                            header.style.setProperty('text-transform', 'uppercase', 'important');
                            header.style.setProperty('letter-spacing', '0.5px', 'important');
                            header.style.setProperty('border-bottom', '1px solid #e5e5ea', 'important');
                            header.style.setProperty('background', '#f8f8fa', 'important');
                            header.style.setProperty('display', 'flex', 'important');
                            header.style.setProperty('align-items', 'center', 'important');
                            header.style.setProperty('justify-content', 'space-between', 'important');

                            const headerTitle = document.createElement('span');
                            headerTitle.textContent = 'Kayıtlı Hesaplar';
                            header.appendChild(headerTitle);

                            const headerBrand = document.createElement('span');
                            headerBrand.style.setProperty('font-size', '10px', 'important');
                            headerBrand.style.setProperty('opacity', '0.8', 'important');
                            headerBrand.textContent = 'Bear Browser';
                            header.appendChild(headerBrand);

                            p.appendChild(header);

                            accounts.forEach((acc) => {
                                const item = document.createElement('div');
                                item.style.setProperty('padding', '10px 14px', 'important');
                                item.style.setProperty('display', 'flex', 'important');
                                item.style.setProperty('align-items', 'center', 'important');
                                item.style.setProperty('gap', '10px', 'important');
                                item.style.setProperty('cursor', 'pointer', 'important');
                                item.style.setProperty('background', '#ffffff', 'important');
                                item.style.setProperty('transition', 'background 0.15s ease', 'important');

                                const iconBox = document.createElement('div');
                                iconBox.style.setProperty('width', '28px', 'important');
                                iconBox.style.setProperty('height', '28px', 'important');
                                iconBox.style.setProperty('border-radius', '50%', 'important');
                                iconBox.style.setProperty('background', '#f2f2f7', 'important');
                                iconBox.style.setProperty('border', '1px solid #e5e5ea', 'important');
                                iconBox.style.setProperty('display', 'flex', 'important');
                                iconBox.style.setProperty('align-items', 'center', 'important');
                                iconBox.style.setProperty('justify-content', 'center', 'important');
                                iconBox.style.setProperty('font-size', '13px', 'important');
                                iconBox.style.setProperty('flex-shrink', '0', 'important');
                                iconBox.textContent = '🔑';
                                item.appendChild(iconBox);

                                const textBox = document.createElement('div');
                                textBox.style.setProperty('display', 'flex', 'important');
                                textBox.style.setProperty('flex-direction', 'column', 'important');
                                textBox.style.setProperty('overflow', 'hidden', 'important');
                                textBox.style.setProperty('flex', '1', 'important');

                                const userSpan = document.createElement('span');
                                userSpan.style.setProperty('font-weight', '500', 'important');
                                userSpan.style.setProperty('color', '#1c1c1e', 'important');
                                userSpan.style.setProperty('white-space', 'nowrap', 'important');
                                userSpan.style.setProperty('overflow', 'hidden', 'important');
                                userSpan.style.setProperty('text-overflow', 'ellipsis', 'important');
                                userSpan.textContent = acc.user || 'İsimsiz Hesap';
                                textBox.appendChild(userSpan);

                                const dotsSpan = document.createElement('span');
                                dotsSpan.style.setProperty('font-size', '11px', 'important');
                                dotsSpan.style.setProperty('color', '#8e8e93', 'important');
                                dotsSpan.style.setProperty('letter-spacing', '1px', 'important');
                                dotsSpan.textContent = '••••••••';
                                textBox.appendChild(dotsSpan);

                                item.appendChild(textBox);

                                item.addEventListener('mouseenter', () => item.style.setProperty('background', '#f2f2f7', 'important'));
                                item.addEventListener('mouseleave', () => item.style.setProperty('background', '#ffffff', 'important'));

                                item.addEventListener('mousedown', (e) => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                });
                                item.addEventListener('mouseup', (e) => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                });
                                item.addEventListener('click', (e) => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    isSelectingAccount = true;
                                    fillAutofillAccount(acc);
                                    hideAutofillPopup();
                                    setTimeout(() => { isSelectingAccount = false; }, 600);
                                });

                                p.appendChild(item);
                            });

                            const rect = target.getBoundingClientRect();
                            const pWidth = Math.max(Math.round(rect.width), 260);
                            p.style.setProperty('width', pWidth + 'px', 'important');
                            p.style.setProperty('left', Math.max(8, Math.round(rect.left)) + 'px', 'important');
                            p.style.setProperty('display', 'block', 'important');

                            const pHeight = p.offsetHeight || 140;
                            const spaceBelow = window.innerHeight - rect.bottom;
                            if (spaceBelow < pHeight && rect.top > pHeight) {
                                p.style.setProperty('top', Math.round(rect.top - pHeight - 4) + 'px', 'important');
                            } else {
                                p.style.setProperty('top', Math.round(rect.bottom + 4) + 'px', 'important');
                            }
                        };

                        window.__bearSetAutofillAccounts = (accList) => {
                            window.__bearAccounts = accList || [];
                            if (currentAutofillInput && window.__bearAccounts.length > 0 && !isSelectingAccount) {
                                showAutofillPopup(currentAutofillInput);
                            }
                        };

                        const isEligibleInput = (el) => {
                            if (!el || el.tagName !== 'INPUT') return false;
                            const type = (el.type || 'text').toLowerCase();
                            if (['hidden', 'submit', 'button', 'checkbox', 'radio', 'file', 'search', 'number', 'range', 'date', 'time', 'color'].indexOf(type) !== -1) {
                                return false;
                            }

                            const attrText = [
                                el.getAttribute('type'),
                                el.getAttribute('name'),
                                el.getAttribute('id'),
                                el.getAttribute('placeholder'),
                                el.getAttribute('aria-label'),
                                el.getAttribute('role'),
                                el.className
                            ].filter(Boolean).join(' ').toLowerCase();

                            // Kesinlikle arama veya sorgu kutusu olmamalı
                            if (attrText.indexOf('search') !== -1 || 
                                attrText.indexOf('query') !== -1 || 
                                attrText.indexOf('find') !== -1 || 
                                attrText.indexOf('filter') !== -1 || 
                                attrText.indexOf('ara') !== -1 || 
                                el.getAttribute('name') === 'q' || 
                                el.getAttribute('role') === 'searchbox' ||
                                (el.closest && el.closest('[role=""search""]'))) {
                                return false;
                            }

                            // Doğrudan parola kutusuysa uygundur
                            if (type === 'password' || attrText.indexOf('password') !== -1 || attrText.indexOf('parola') !== -1 || attrText.indexOf('sifre') !== -1) {
                                return true;
                            }

                            // Kullanıcı adı / e-posta alanları
                            const auto = (el.getAttribute('autocomplete') || '').toLowerCase();
                            if (auto.indexOf('username') !== -1 || auto.indexOf('email') !== -1 || auto.indexOf('current-password') !== -1) {
                                return true;
                            }

                            if (attrText.indexOf('user') !== -1 || attrText.indexOf('login') !== -1 || attrText.indexOf('email') !== -1 || attrText.indexOf('eposta') !== -1 || attrText.indexOf('kullanici') !== -1 || attrText.indexOf('account') !== -1) {
                                return true;
                            }

                            // Genel metin kutusuysa sadece sayfada veya formda bir parola kutusu varsa uygundur
                            const container = (el.form || (el.closest && el.closest('form'))) || document;
                            const hasPasswordInForm = !!container.querySelector('input[type=""password""]');
                            return hasPasswordInForm;
                        };

                        const isClickOnOrNearInput = (el, clientX, clientY) => {
                            if (!el || !el.getBoundingClientRect) return false;
                            const r = el.getBoundingClientRect();
                            if (r.width === 0 && r.height === 0) return false;
                            return (
                                clientX >= r.left - 15 &&
                                clientX <= r.right + 15 &&
                                clientY >= r.top - 15 &&
                                clientY <= r.bottom + 15
                            );
                        };

                        const resolveCandidateInput = (el, clientX, clientY) => {
                            if (!el) return null;

                            if (isEligibleInput(el)) return el;

                            const active = document.activeElement;
                            if (isEligibleInput(active) && (active === el || active.contains(el) || (clientX !== undefined && isClickOnOrNearInput(active, clientX, clientY)))) {
                                return active;
                            }

                            if (el.tagName === 'LABEL' && el.htmlFor) {
                                const target = document.getElementById(el.htmlFor);
                                if (isEligibleInput(target)) return target;
                            }
                            const parentLabel = el.closest ? el.closest('label') : null;
                            if (parentLabel) {
                                if (parentLabel.htmlFor) {
                                    const target = document.getElementById(parentLabel.htmlFor);
                                    if (isEligibleInput(target)) return target;
                                }
                                const inner = parentLabel.querySelector('input');
                                if (isEligibleInput(inner)) return inner;
                            }

                            let cur = el;
                            for (let i = 0; i < 4 && cur && cur !== document.body && cur !== document.documentElement; i++) {
                                const found = cur.querySelector && cur.querySelector('input');
                                if (isEligibleInput(found)) {
                                    const rect = cur.getBoundingClientRect();
                                    if (rect.height < 120 && (clientX === undefined || isClickOnOrNearInput(found, clientX, clientY))) {
                                        return found;
                                    }
                                }
                                cur = cur.parentElement;
                            }

                            return null;
                        };

                        document.addEventListener('focusin', (e) => {
                            if (isSelectingAccount || justDismissed) return;
                            const input = resolveCandidateInput(e.target);
                            if (input) {
                                showAutofillPopup(input);
                            }
                        }, true);

                        document.addEventListener('click', (e) => {
                            if (isSelectingAccount || justDismissed) return;
                            const p = document.getElementById('bear-autofill-popup');
                            if (p && p.contains(e.target)) return;

                            const input = resolveCandidateInput(e.target, e.clientX, e.clientY);
                            if (input) {
                                showAutofillPopup(input);
                            } else {
                                if (p && p.style.display !== 'none') {
                                    hideAutofillPopup();
                                }
                            }
                        }, true);

                        document.addEventListener('mousedown', (e) => {
                            if (isSelectingAccount) return;
                            const p = document.getElementById('bear-autofill-popup');
                            if (p && p.style.display !== 'none') {
                                if (p.contains(e.target)) return;
                                const input = resolveCandidateInput(e.target, e.clientX, e.clientY);
                                if (!input) {
                                    hideAutofillPopup();
                                    justDismissed = true;
                                    setTimeout(() => { justDismissed = false; }, 250);
                                }
                            }
                        }, true);

                        document.addEventListener('keydown', (e) => {
                            if (e.key === 'Escape') hideAutofillPopup();
                        }, true);

                        window.addEventListener('wheel', () => hideAutofillPopup(), { passive: true });
                        window.addEventListener('scroll', () => hideAutofillPopup(), { passive: true });
                        window.addEventListener('resize', () => hideAutofillPopup());

                        // --- BEAR BROWSER EKLENTİ MAĞAZASI ENTEGRASYONU ---
                        try {
                            const isChromeStore = window.location.hostname.indexOf('chromewebstore.google.com') !== -1 || window.location.hostname.indexOf('chrome.google.com') !== -1;
                            const isEdgeStore = window.location.hostname.indexOf('microsoftedge.microsoft.com') !== -1;

                            if (isChromeStore || isEdgeStore) {
                                // 1. Chrome Web Store dahili API köprüsü
                                try {
                                    window.chrome = window.chrome || {};
                                    window.chrome.webstorePrivate = {
                                        beginInstallWithManifest3: function(details, callback) {
                                            let id = (details && details.id) ? details.id : '';
                                            if (!id) {
                                                const m = (window.location.pathname || '').match(/([a-p]{32})/i);
                                                if (m) id = m[1];
                                            }
                                            sendToNative('install_extension:' + id + '|||' + document.title);
                                            if (typeof callback === 'function') callback();
                                        },
                                        install: function(id, callback) {
                                            sendToNative('install_extension:' + id + '|||' + document.title);
                                            if (typeof callback === 'function') callback();
                                        },
                                        completeInstall: function(id, callback) {
                                            if (typeof callback === 'function') callback();
                                        }
                                    };
                                } catch(errBridge) {}

                                const getExtId = () => {
                                    const path = window.location.pathname || '';
                                    if (isChromeStore) {
                                        const m = path.match(/([a-p]{32})/i);
                                        return m ? m[1].toLowerCase() : '';
                                    } else if (isEdgeStore) {
                                        const m = path.match(/([a-z0-9]{32})/i);
                                        return m ? m[1].toLowerCase() : '';
                                    }
                                    return '';
                                };

                                // 2. Sayfadaki orijinal Chrome'a ekle / Al butonlarına tıklandığında yakala
                                document.addEventListener('click', (e) => {
                                    try {
                                        const target = e.target;
                                        if (!target) return;

                                        const btn = target.closest('button, [role=""button""]');
                                        if (!btn || btn.id === 'bear-install-extension-btn') return;

                                        const txt = (btn.innerText || btn.textContent || '').trim().toLowerCase();
                                        const aria = (btn.getAttribute('aria-label') || '').toLowerCase();

                                        const isInstallBtn = (txt.indexOf('chrome') !== -1 && txt.indexOf('ekle') !== -1) ||
                                                             txt.indexOf('add to chrome') !== -1 ||
                                                             txt === 'al' || txt === 'get' ||
                                                             (aria.indexOf('chrome') !== -1 && aria.indexOf('ekle') !== -1) ||
                                                             aria.indexOf('add to chrome') !== -1 ||
                                                             aria.indexOf('get') !== -1;

                                        if (isInstallBtn) {
                                            const extId = getExtId();
                                            if (extId) {
                                                e.preventDefault();
                                                e.stopPropagation();

                                                let title = document.title.split('-')[0].trim();
                                                const h1 = document.querySelector('h1');
                                                if (h1 && h1.innerText) title = h1.innerText.trim();

                                                btn.style.opacity = '0.7';
                                                btn.innerText = '🐻 Bear Browser İle Ekleniyor...';

                                                sendToNative('install_extension:' + extId + '|||' + title);
                                            }
                                        }
                                    } catch(errClick) {}
                                }, true);

                                // 3. Sağ üst köşeye doğrudan buton yerleştir
                                const injectInstallBtn = () => {
                                    if (document.getElementById('bear-install-extension-btn')) return;

                                    const extId = getExtId();
                                    if (!extId) return;

                                    let title = document.title.split('-')[0].trim();
                                    const h1 = document.querySelector('h1');
                                    if (h1 && h1.innerText) title = h1.innerText.trim();

                                    const btn = document.createElement('button');
                                    btn.id = 'bear-install-extension-btn';
                                    btn.innerHTML = '🐻 Bear Browser Ekle';
                                    btn.style.setProperty('position', 'fixed', 'important');
                                    btn.style.setProperty('top', '80px', 'important');
                                    btn.style.setProperty('right', '32px', 'important');
                                    btn.style.setProperty('z-index', '2147483647', 'important');
                                    btn.style.setProperty('background', '#0078D4', 'important');
                                    btn.style.setProperty('color', '#ffffff', 'important');
                                    btn.style.setProperty('font-size', '14px', 'important');
                                    btn.style.setProperty('font-weight', '600', 'important');
                                    btn.style.setProperty('padding', '10px 22px', 'important');
                                    btn.style.setProperty('border', 'none', 'important');
                                    btn.style.setProperty('border-radius', '20px', 'important');
                                    btn.style.setProperty('box-shadow', '0 6px 20px rgba(0, 120, 212, 0.45)', 'important');
                                    btn.style.setProperty('cursor', 'pointer', 'important');
                                    btn.style.setProperty('transition', 'all 0.2s ease', 'important');
                                    btn.style.setProperty('font-family', '-apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif', 'important');

                                    btn.onmouseenter = () => btn.style.setProperty('background', '#106EBE', 'important');
                                    btn.onmouseleave = () => btn.style.setProperty('background', '#0078D4', 'important');

                                    btn.onclick = () => {
                                        btn.innerHTML = '⏳ İndiriliyor ve Kuruluyor...';
                                        btn.disabled = true;
                                        sendToNative('install_extension:' + extId + '|||' + title);
                                    };

                                    document.body.appendChild(btn);
                                };

                                if (document.readyState === 'loading') {
                                    document.addEventListener('DOMContentLoaded', injectInstallBtn);
                                } else {
                                    setTimeout(injectInstallBtn, 800);
                                }

                                let lastUrl = location.href;
                                setInterval(() => {
                                    if (location.href !== lastUrl) {
                                        lastUrl = location.href;
                                        const old = document.getElementById('bear-install-extension-btn');
                                        if (old) old.remove();
                                        setTimeout(injectInstallBtn, 800);
                                    }
                                }, 1000);
                            }
                        } catch(e) {}

                        try {
                            sendToNative('request_autofill:' + window.location.href);
                        } catch(e) {}
                    })();
                ");
            }
        }
    }
}