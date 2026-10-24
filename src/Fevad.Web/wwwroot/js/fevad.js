// Kleine Helfer für Formulare. Datumswerte als "yyyy-MM-dd", wie sie <input type="date"> liefert.

/** Datum plus Monate wie DateOnly.AddMonths: am Monatsende auf den letzten Tag gekürzt. Leer bei fehlenden Angaben. */
window.plusMonate = function (datum, monate) {
    if (!datum || !monate) return "";
    const [j, m, t] = datum.split("-").map(Number);
    const ziel = new Date(j, m - 1 + Number(monate), 1);
    const letzter = new Date(ziel.getFullYear(), ziel.getMonth() + 1, 0).getDate();
    ziel.setDate(Math.min(t, letzter));
    const zwei = (n) => String(n).padStart(2, "0");
    return `${ziel.getFullYear()}-${zwei(ziel.getMonth() + 1)}-${zwei(ziel.getDate())}`;
};
