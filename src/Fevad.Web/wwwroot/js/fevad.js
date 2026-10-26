// Kleine Helfer für Formulare. Datumsfelder zeigen "TT.MM.JJJJ" (DatumTagHelper); der Server liest auch Kurzformen und ISO.

/** "24.10.2026", "24.10.26", "24102026", "24.10." oder ISO → "yyyy-MM-dd"; "" wenn kein Datum. */
window.datumIso = function (text) {
    if (!text) return "";
    const t = String(text).trim();
    let m;
    if ((m = t.match(/^(\d{4})-(\d{2})-(\d{2})$/))) return t;
    if ((m = t.match(/^(\d{1,2})\.(\d{1,2})\.(\d{2}|\d{4})?$/)) || (m = t.match(/^(\d{2})(\d{2})(\d{2}|\d{4})$/))) {
        const heute = new Date();
        let j = m[3] === undefined ? heute.getFullYear() : Number(m[3]);
        if (m[3] !== undefined && m[3].length === 2) j += 2000;
        const d = new Date(j, Number(m[2]) - 1, Number(m[1]));
        if (d.getFullYear() !== j || d.getMonth() !== Number(m[2]) - 1 || d.getDate() !== Number(m[1])) return "";
        const zwei = (n) => String(n).padStart(2, "0");
        return `${j}-${zwei(d.getMonth() + 1)}-${zwei(d.getDate())}`;
    }
    return "";
};

/** "yyyy-MM-dd" → "TT.MM.JJJJ". */
window.datumText = function (iso) {
    if (!iso) return "";
    const [j, m, t] = iso.split("-");
    return `${t}.${m}.${j}`;
};

/** Datum plus Monate wie DateOnly.AddMonths: am Monatsende auf den letzten Tag gekürzt. Nimmt und liefert "TT.MM.JJJJ"; leer bei fehlenden Angaben. */
window.plusMonate = function (datum, monate) {
    const iso = window.datumIso(datum);
    if (!iso || !monate) return "";
    const [j, m, t] = iso.split("-").map(Number);
    const ziel = new Date(j, m - 1 + Number(monate), 1);
    const letzter = new Date(ziel.getFullYear(), ziel.getMonth() + 1, 0).getDate();
    ziel.setDate(Math.min(t, letzter));
    const zwei = (n) => String(n).padStart(2, "0");
    return window.datumText(`${ziel.getFullYear()}-${zwei(ziel.getMonth() + 1)}-${zwei(ziel.getDate())}`);
};

// Datumsfelder: Kalenderknopf öffnet die native Auswahl, Auswahl landet als Text im Feld; beim Verlassen wird die Eingabe
// in "TT.MM.JJJJ" gebracht ("h" = heute), Unlesbares wird rot markiert (der Server prüft trotzdem).
function datumsfelderEinrichten(wurzel) {
    for (const feld of (wurzel || document).querySelectorAll("input[data-datum]:not([data-datum-bereit])")) {
        feld.dataset.datumBereit = "1";
        const huelle = feld.parentElement;
        const nativ = huelle?.querySelector(".fevad-datum-nativ");
        const knopf = huelle?.querySelector(".fevad-datum-knopf");
        if (!nativ || !knopf) continue;
        knopf.addEventListener("click", () => {
            if (feld.disabled || feld.readOnly) return;
            nativ.value = window.datumIso(feld.value);
            try { nativ.showPicker(); } catch { nativ.focus(); nativ.click(); }
        });
        nativ.addEventListener("change", () => {
            feld.value = window.datumText(nativ.value);
            feld.classList.remove("ungueltig");
            feld.dispatchEvent(new Event("input", { bubbles: true }));
            feld.dispatchEvent(new Event("change", { bubbles: true }));
        });
        feld.addEventListener("blur", () => {
            const t = feld.value.trim();
            if (!t) { feld.classList.remove("ungueltig"); return; }
            const iso = /^h(eute)?$/i.test(t) ? new Date().toLocaleDateString("sv-SE") : window.datumIso(t);
            if (iso) {
                const neu = window.datumText(iso);
                if (neu !== feld.value) {
                    feld.value = neu;
                    feld.dispatchEvent(new Event("input", { bubbles: true }));
                }
                feld.classList.remove("ungueltig");
            } else {
                feld.classList.add("ungueltig");
            }
        });
    }
}

document.addEventListener("DOMContentLoaded", () => datumsfelderEinrichten(document));
document.addEventListener("htmx:afterSwap", (e) => datumsfelderEinrichten(e.target));

// Kamera mit Barcode-Erkennung: ruft beiCode je erkanntem Etikett auf, solange laeuft() wahr ist.
// Dasselbe Etikett bleibt im Bild; es zählt erst nach drei Sekunden wieder.
window.barcodeKamera = async function (video, beiCode, laeuft, fehler) {
    try {
        video.srcObject = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
        await video.play();
        const unterstuetzt = await BarcodeDetector.getSupportedFormats();
        const erkenner = new BarcodeDetector({ formats: ["qr_code", "code_128", "code_39", "data_matrix"].filter((f) => unterstuetzt.includes(f)) });
        const zuletzt = new Map();
        const suchen = async () => {
            if (!laeuft()) return;
            try {
                for (const code of await erkenner.detect(video)) {
                    if (Date.now() - (zuletzt.get(code.rawValue) || 0) < 3000) continue;
                    zuletzt.set(code.rawValue, Date.now());
                    await beiCode(code.rawValue);
                }
            } catch { /* einzelnes Bild nicht lesbar */ }
            setTimeout(suchen, 250);
        };
        setTimeout(suchen, 0);
        return true;
    } catch (e) {
        fehler(`Kamera nicht verfügbar: ${e.message}`);
        kameraAus(video);
        return false;
    }
};

window.kameraAus = function (video) {
    video.srcObject?.getTracks().forEach((spur) => spur.stop());
    video.srcObject = null;
};

// Inventarnummer aus einem Scan: QR-Etiketten enthalten die Adresse …/g/<Nummer> (wie Geraetecode.NummerAusScan).
window.nummerAusScan = function (eingabe) {
    let text = (eingabe || "").trim();
    const stelle = text.toLowerCase().lastIndexOf("/g/");
    if (stelle >= 0) {
        text = decodeURIComponent(text.substring(stelle + 3).split(/[?#]/)[0].replace(/\/+$/, ""));
    }
    return text.trim();
};

// Beladekontrolle (V59): gescannte Geräte werden in der Liste abgehakt; gespeichert wird erst mit dem Formular.
// Auch für die Abgabe an die Werkstatt (V61): Scan hakt das Gerät in der Liste an; fehltext ersetzt die Meldung für fremde Nummern.
window.beladekontrolle = function (nummern, fehltext) {
    return {
        eingabe: "",
        meldung: "",
        kamera: false,
        kameraMoeglich: "BarcodeDetector" in window && !!navigator.mediaDevices,

        nachschlagen() {
            const nummer = nummerAusScan(this.eingabe);
            this.eingabe = "";
            if (!nummer) return;
            const id = nummern[nummer.toUpperCase()];
            const feld = id && document.getElementById(`g-${id}`);
            if (!feld) {
                this.meldung = `${nummer}: ${fehltext || "gehört nicht zur Beladung dieses Fahrzeugs."}`;
                return;
            }
            this.meldung = feld.checked ? `${nummer}: schon abgehakt.` : `${nummer}: gefunden.`;
            feld.checked = true;
        },
        async kameraStarten() {
            this.kamera = await barcodeKamera(this.$refs.video, async (wert) => {
                this.eingabe = wert;
                this.nachschlagen();
            }, () => this.kamera, (text) => { this.meldung = text; });
        },
        kameraStoppen() {
            this.kamera = false;
            kameraAus(this.$refs.video);
        },
    };
};

// Sammelprüfung (V58): Liste der Geräte im Browser aufbauen; gespeichert wird erst mit dem Formular.
window.sammelpruefung = function (start) {
    return {
        zeilen: (start.zeilen || []).map((z) => ({ ...z, mangel: z.mangel || "" })),
        pruefart: start.pruefart,
        eingabe: "",
        meldung: "",
        fahrzeug: "",
        fach: "",
        kamera: false,
        kameraMoeglich: "BarcodeDetector" in window && !!navigator.mediaDevices,

        async abfragen(handler, werte) {
            const parameter = new URLSearchParams({ handler, pruefart: this.pruefart, ...werte });
            const antwort = await fetch(`${location.pathname}?${parameter}`, { headers: { Accept: "application/json" } });
            const daten = await antwort.json().catch(() => ({}));
            if (!antwort.ok) {
                this.meldung = daten.meldung || "Nicht gefunden.";
                return null;
            }
            return daten;
        },
        async nachschlagen() {
            const nummer = this.eingabe.trim();
            this.eingabe = "";
            if (!nummer) return;
            const geraet = await this.abfragen("Geraet", { nummer });
            if (geraet) this.aufnehmen([geraet]);
        },
        async fachLaden() {
            const liste = await this.abfragen("Fach", { fahrzeugId: this.fahrzeug, lagerort: this.fach });
            if (liste) this.aufnehmen(liste);
        },
        async gesperrteLaden() {
            const liste = await this.abfragen("Gesperrte", {});
            if (liste) this.aufnehmen(liste);
        },
        aufnehmen(liste) {
            let neu = 0;
            for (const g of liste) {
                if (this.zeilen.some((z) => z.id === g.id)) continue;
                this.zeilen.push({ ...g, ergebnis: "Bestanden", mangel: "" });
                neu++;
            }
            const letzte = liste.length === 1 ? `${liste[0].nummer} ${liste[0].bezeichnung}: ` : "";
            this.meldung = liste.length === 0 ? "Keine Geräte gefunden." : neu === 0 ? `${letzte}schon in der Liste.` : `${letzte}${neu} hinzugefügt.`;
        },
        entfernen(i) {
            this.zeilen.splice(i, 1);
        },
        async kameraStarten() {
            this.kamera = await barcodeKamera(this.$refs.video, async (wert) => {
                this.eingabe = wert;
                await this.nachschlagen();
            }, () => this.kamera, (text) => { this.meldung = text; });
        },
        kameraStoppen() {
            this.kamera = false;
            kameraAus(this.$refs.video);
        },
    };
};

// Sammelpflege (V63): Einzelstücke scannen oder die Einsatzkleidung eines Einsatzes laden; gespeichert wird erst mit dem Formular.
window.sammelpflege = function () {
    return {
        zeilen: [],
        eingabe: "",
        einsatz: "",
        meldung: "",
        kamera: false,
        kameraMoeglich: "BarcodeDetector" in window && !!navigator.mediaDevices,

        async abfragen(handler, werte) {
            const parameter = new URLSearchParams({ handler, ...werte });
            const antwort = await fetch(`${location.pathname}?${parameter}`, { headers: { Accept: "application/json" } });
            const daten = await antwort.json().catch(() => ({}));
            if (!antwort.ok) {
                this.meldung = daten.meldung || "Nicht gefunden.";
                return null;
            }
            return daten;
        },
        async nachschlagen() {
            const nummer = this.eingabe.trim();
            this.eingabe = "";
            if (!nummer) return;
            const stueck = await this.abfragen("Geraet", { nummer });
            if (stueck) this.aufnehmen([stueck]);
        },
        async einsatzLaden() {
            const liste = await this.abfragen("Einsatz", { einsatzId: this.einsatz });
            if (liste) this.aufnehmen(liste);
        },
        aufnehmen(liste) {
            let neu = 0;
            for (const s of liste) {
                if (this.zeilen.some((z) => z.id === s.id)) continue;
                this.zeilen.push(s);
                neu++;
            }
            const letzte = liste.length === 1 ? `${liste[0].nummer} ${liste[0].bezeichnung}: ` : "";
            this.meldung = liste.length === 0 ? "Keine Einzelstücke gefunden." : neu === 0 ? `${letzte}schon in der Liste.` : `${letzte}${neu} hinzugefügt.`;
        },
        async kameraStarten() {
            this.kamera = await barcodeKamera(this.$refs.video, async (wert) => {
                this.eingabe = wert;
                await this.nachschlagen();
            }, () => this.kamera, (text) => { this.meldung = text; });
        },
        kameraStoppen() {
            this.kamera = false;
            kameraAus(this.$refs.video);
        },
    };
};

// CSV-Export der sichtbaren Spalten (V70): jede Tabelle mit Klasse "tabelle" und mindestens drei Zeilen bekommt einen Knopf "CSV".
// Exportiert wird, was die Seite zeigt (die Rechte hat der Server schon geprüft); Spalten ohne Überschrift (Knöpfe) entfallen.
function csvKnoepfeEinrichten(wurzel) {
    const tabellen = [...(wurzel || document).querySelectorAll("table.tabelle:not([data-csv-bereit]):not([data-kein-csv])")];
    if (wurzel?.matches?.("table.tabelle:not([data-csv-bereit]):not([data-kein-csv])")) tabellen.push(wurzel);
    for (const tabelle of tabellen) {
        tabelle.dataset.csvBereit = "1";
        const koepfe = [...(tabelle.tHead?.rows[0]?.cells || [])];
        if (koepfe.length === 0 || tabelle.tBodies[0]?.rows.length < 3) continue;
        const leiste = document.createElement("div");
        leiste.className = "flex justify-end px-2 pt-1 print:hidden";
        const knopf = document.createElement("button");
        knopf.type = "button";
        knopf.className = "text-xs text-feuer-700 hover:underline";
        knopf.textContent = "CSV";
        knopf.setAttribute("aria-label", "Tabelle als CSV herunterladen");
        knopf.addEventListener("click", () => csvHerunterladen(tabelle));
        leiste.append(knopf);
        tabelle.before(leiste);
    }
}

function csvHerunterladen(tabelle) {
    const sichtbar = (zelle) => zelle && zelle.offsetParent !== null && getComputedStyle(zelle).visibility !== "hidden";
    const text = (zelle) => (zelle?.innerText || "").replace(/\s+/g, " ").trim();
    const koepfe = [...tabelle.tHead.rows[0].cells];
    const spalten = koepfe.map((k, i) => i).filter((i) => sichtbar(koepfe[i]) && text(koepfe[i]) !== "");
    const feld = (wert) => /[";\n]/.test(wert) ? `"${wert.replace(/"/g, '""')}"` : wert;
    const zeilen = [spalten.map((i) => feld(text(koepfe[i])))];
    for (const zeile of tabelle.tBodies[0].rows) {
        if (zeile.offsetParent === null || zeile.cells.length < koepfe.length) continue;
        zeilen.push(spalten.map((i) => feld(text(zeile.cells[i]))));
    }
    const name = (document.title.split("·").slice(0, -1).join(" ").trim() || "Liste").replace(/[^\p{L}\p{N}]+/gu, "-").replace(/^-|-$/g, "");
    const datei = new Blob(["﻿" + zeilen.map((z) => z.join(";")).join("\r\n")], { type: "text/csv;charset=utf-8" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(datei);
    link.download = `${name}.csv`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

document.addEventListener("DOMContentLoaded", () => csvKnoepfeEinrichten(document));
document.addEventListener("htmx:afterSwap", (e) => csvKnoepfeEinrichten(e.target));
