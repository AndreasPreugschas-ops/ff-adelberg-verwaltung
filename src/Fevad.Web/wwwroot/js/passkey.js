// Passkeys (V41): Anmeldung und Einrichtung über die WebAuthn-Schnittstelle des Browsers.
// Ohne Unterstützung im Browser bleiben die Passkey-Knöpfe verborgen; Passwort und Authenticator-App gehen immer.
(function () {
    const unterstuetzt = window.PublicKeyCredential
        && typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function'
        && typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function';

    function token(form) {
        const feld = form.querySelector('input[name="__RequestVerificationToken"]');
        return feld ? feld.value : '';
    }

    async function optionen(form, handler) {
        const url = new URL(form.action || window.location.href, window.location.origin);
        url.searchParams.set('handler', handler);
        const antwort = await fetch(url, { method: 'POST', headers: { RequestVerificationToken: token(form) }, credentials: 'same-origin' });
        if (!antwort.ok) {
            throw new Error('Optionen nicht erhalten (' + antwort.status + ')');
        }
        return antwort.json();
    }

    document.querySelectorAll('[data-passkey-anmelden]').forEach(function (form) {
        if (!unterstuetzt) {
            return;
        }
        form.classList.remove('hidden');
        form.classList.add('grid');
        form.querySelector('[data-passkey-start]').addEventListener('click', async function () {
            try {
                const json = await optionen(form, 'PasskeyOptionen');
                const nachweis = await navigator.credentials.get({ publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(json) });
                form.querySelector('input[name="credentialJson"]').value = JSON.stringify(nachweis);
                const merken = document.querySelector('input[name="Formular.Merken"]');
                form.querySelector('input[name="merken"]').value = merken && merken.checked ? 'true' : 'false';
                form.submit();
            } catch (fehler) {
                console.warn('Passkey-Anmeldung abgebrochen', fehler);
            }
        });
    });

    document.querySelectorAll('[data-passkey-einrichten]').forEach(function (form) {
        const hinweis = form.querySelector('[data-passkey-hinweis]');
        if (!unterstuetzt) {
            form.querySelector('button').disabled = true;
            if (hinweis) {
                hinweis.textContent = 'Dieser Browser unterstützt keine Passkeys. Bitte weiter Passwort und Authenticator-App verwenden.';
            }
            return;
        }
        form.addEventListener('submit', async function (ereignis) {
            if (form.querySelector('input[name="credentialJson"]').value) {
                return;
            }
            ereignis.preventDefault();
            try {
                const json = await optionen(form, 'Optionen');
                const neu = await navigator.credentials.create({ publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(json) });
                form.querySelector('input[name="credentialJson"]').value = JSON.stringify(neu);
                form.submit();
            } catch (fehler) {
                if (hinweis) {
                    hinweis.textContent = 'Einrichtung abgebrochen oder auf diesem Gerät nicht möglich.';
                }
                console.warn('Passkey-Einrichtung abgebrochen', fehler);
            }
        });
    });
})();
