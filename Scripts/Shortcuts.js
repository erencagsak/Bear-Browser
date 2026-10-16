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
})();
