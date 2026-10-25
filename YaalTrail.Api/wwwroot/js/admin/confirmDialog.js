// A confirmation <dialog> (FR-41: deleting needs explicit confirmation).
// The native dialog traps focus, closes on Escape and returns focus afterwards.
import { el } from '../dom.js';
import { ADMIN_STRINGS } from './adminStrings.js';

// Resolves true when the admin presses the confirm button, false otherwise.
// lines: paragraphs of text shown under the title.
export function confirmAction(dialog, { title, lines, confirmLabel, danger = true }) {
    return new Promise((resolve) => {
        const opener = document.activeElement;
        const cancel = el('button', { type: 'button', className: 'button button-secondary', onClick: () => dialog.close('cancel') }, [ADMIN_STRINGS.cancel]);
        const confirm = el('button', {
            type: 'button',
            className: `button ${danger ? 'button-danger' : 'button-primary'}`,
            onClick: () => dialog.close('confirm'),
        }, [confirmLabel]);

        dialog.replaceChildren(
            el('h2', { id: 'confirm-title', className: 'dialog-title', text: title }),
            ...lines.filter(Boolean).map((text) => el('p', { text })),
            el('div', { className: 'dialog-actions' }, [cancel, confirm]),
        );
        dialog.setAttribute('aria-labelledby', 'confirm-title');

        dialog.addEventListener('close', () => {
            resolve(dialog.returnValue === 'confirm');
            if (opener?.isConnected) opener.focus();
        }, { once: true });

        dialog.returnValue = '';
        dialog.showModal();
        cancel.focus(); // the safe choice has focus first
    });
}
