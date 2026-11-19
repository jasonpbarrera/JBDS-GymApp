// Lightweight camera QR & Barcode scanner using native browser BarcodeDetector API
// with graceful fallback to USB/manual scanner input.
window.jbdsQrScanner = (() => {
    let stream = null;
    let rafId = null;
    let active = false;

    async function start(videoElementId, dotNetHelper) {
        stop();

        if (!('BarcodeDetector' in window)) {
            return {
                supported: false,
                message: "Live camera BarcodeDetector is not supported in this browser (use Chrome/Edge/Android or a USB/manual scanner)."
            };
        }

        const video = document.getElementById(videoElementId);
        if (!video) {
            return { supported: false, message: "Camera preview element not found." };
        }

        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: "environment" },
                audio: false
            });
            video.srcObject = stream;
            await video.play();

            const detector = new window.BarcodeDetector({
                formats: ["qr_code", "code_128", "ean_13", "upc_a", "code_39"]
            });

            active = true;
            let lastCode = "";
            let lastScanAt = 0;

            const tick = async () => {
                if (!active) return;
                try {
                    if (video.readyState >= 2) {
                        const barcodes = await detector.detect(video);
                        if (barcodes && barcodes.length > 0) {
                            const code = barcodes[0].rawValue;
                            const now = Date.now();
                            if (code && (code !== lastCode || now - lastScanAt > 3000)) {
                                lastCode = code;
                                lastScanAt = now;
                                await dotNetHelper.invokeMethodAsync("OnBarcodeDetected", code);
                            }
                        }
                    }
                } catch {
                    // Ignore transient frame decode errors
                }
                if (active) {
                    rafId = requestAnimationFrame(tick);
                }
            };

            rafId = requestAnimationFrame(tick);
            return { supported: true, message: "Camera scanner active." };
        } catch (err) {
            stop();
            return { supported: false, message: `Camera error: ${err?.message || err}` };
        }
    }

    function stop() {
        active = false;
        if (rafId) {
            cancelAnimationFrame(rafId);
            rafId = null;
        }
        if (stream) {
            stream.getTracks().forEach(t => t.stop());
            stream = null;
        }
    }

    return { start, stop };
})();
