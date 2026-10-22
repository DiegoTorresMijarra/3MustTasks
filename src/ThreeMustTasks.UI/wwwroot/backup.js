// Descarga de texto como fichero (backup JSON, export .ics). Se carga como módulo ES desde BackupPanel e IcsExportPanel.
export function downloadText(fileName, text, mimeType = "application/json") {
    const blob = new Blob([text], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
