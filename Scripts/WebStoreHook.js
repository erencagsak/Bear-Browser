(() => {
    const nativeBridge = window.chrome?.webview;
    const sendToNative = (msg) => {
        try {
            if (nativeBridge && nativeBridge.postMessage) {
                nativeBridge.postMessage(msg);
            }
        } catch(e) {}
    };

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

                    const btn = target.closest('button, [role="button"]');
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
})();
