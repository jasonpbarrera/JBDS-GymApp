window.jbdsTheme = {
    apply: function (themePreset, primaryColor, accentColor) {
        const root = document.documentElement;
        root.setAttribute("data-theme", themePreset || "iron-dark");
        if (primaryColor) root.style.setProperty("--brand-primary", primaryColor);
        if (accentColor) root.style.setProperty("--brand-accent", accentColor);
    },
    downloadTextFile: function (filename, content, mimeType) {
        const blob = new Blob([content], { type: mimeType || "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
};
