// Photos of one place (FR-11, FR-45): thumbnails with up/down/delete, and an upload form.
// Every action is sent to the server straight away; it is not part of "Save place".
import { el } from '../dom.js';
import { ADMIN_STRINGS as S } from './adminStrings.js';
import { deletePlaceImage, reorderPlaceImages, uploadPlaceImage } from './adminApi.js';
import { confirmAction } from './confirmDialog.js';
import { errorMessage } from './adminFormat.js';

export const MAX_PHOTOS = 5;                    // same limits as AdminPlaceImageService
export const MAX_FILE_BYTES = 3 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const ALLOWED_EXTENSIONS = /\.(jpe?g|png|webp)$/i;

// place = AdminPlaceDto. ctx = { dialog }
export function createPhotoEditor(place, ctx) {
    let images = [...place.images].sort((a, b) => a.sortOrder - b.sortOrder);

    const title = el('h2', { id: 'photos-title', className: 'section-title', tabindex: '-1' });
    const list = el('ul', { className: 'photo-list' });
    const status = el('p', { className: 'message message-info live-region', role: 'status' });
    const alert = el('p', { className: 'message message-error live-region', role: 'alert' });
    const upload = buildUploadForm(onUpload);

    const element = el('section', { className: 'form-section photo-editor', 'aria-labelledby': 'photos-title' }, [
        title,
        el('p', { className: 'hint', text: S.photosHint }),
        list,
        status,
        alert,
        upload.element,
    ]);
    render();
    return element;

    function render() {
        // Keep keyboard focus on the same button after the list is rebuilt.
        const focusKey = list.contains(document.activeElement) ? document.activeElement.dataset.focusKey : null;

        title.textContent = S.photosTitle(images.length, MAX_PHOTOS);
        list.replaceChildren(...(images.length === 0
            ? [el('li', { className: 'empty', text: S.noPhotos })]
            : images.map((image, index) => photoItem(image, index))));
        upload.setFull(images.length >= MAX_PHOTOS);

        if (focusKey) {
            const target = list.querySelector(`[data-focus-key="${focusKey}"]:not(:disabled)`);
            (target ?? title).focus?.();
        }
    }

    function photoItem(image, index) {
        const n = index + 1;
        return el('li', { className: 'photo-item' }, [
            thumbnail(image, n),
            el('div', { className: 'photo-text' }, [
                index === 0 ? el('span', { className: 'cover-badge', text: S.coverPhoto }) : null,
                el('span', { className: image.caption ? null : 'muted', text: image.caption || S.noCaption }),
            ]),
            el('div', { className: 'row-actions' }, [
                el('button', {
                    type: 'button', className: 'button button-secondary button-small',
                    'aria-label': S.movePhotoEarlier(n), 'data-focus-key': `up-${image.id}`,
                    disabled: index === 0, onClick: () => move(index, -1),
                }, [el('span', { 'aria-hidden': 'true', text: '↑' })]),
                el('button', {
                    type: 'button', className: 'button button-secondary button-small',
                    'aria-label': S.movePhotoLater(n), 'data-focus-key': `down-${image.id}`,
                    disabled: index === images.length - 1, onClick: () => move(index, 1),
                }, [el('span', { 'aria-hidden': 'true', text: '↓' })]),
                el('button', {
                    type: 'button', className: 'button button-danger-outline button-small',
                    'aria-label': S.deletePhotoLabel(n), onClick: () => remove(image),
                }, [S.delete]),
            ]),
        ]);
    }

    // NFR-24: a broken image becomes a text placeholder instead of an empty box.
    function thumbnail(image, n) {
        const img = el('img', {
            className: 'photo-thumb',
            src: image.imageUrl,
            alt: image.caption || S.photoAlt(n, place.name),
            loading: 'lazy',
            onError: () => img.replaceWith(el('span', { className: 'photo-thumb photo-broken', text: S.photoBroken })),
        });
        return img;
    }

    async function move(index, delta) {
        const order = images.map((image) => image.id);
        [order[index], order[index + delta]] = [order[index + delta], order[index]];
        clearMessages();
        try {
            images = await reorderPlaceImages(place.id, order);
            render();
            status.textContent = S.photosReordered;
        } catch (error) {
            if (error.status !== 401) alert.textContent = errorMessage(error);
        }
    }

    async function remove(image) {
        const confirmed = await confirmAction(ctx.dialog, {
            title: S.deletePhotoTitle,
            lines: [S.deletePhotoText],
            confirmLabel: S.deletePhotoConfirm,
        });
        if (!confirmed) return;

        clearMessages();
        try {
            await deletePlaceImage(place.id, image.id);
            images = images.filter((other) => other.id !== image.id);
            render();
            title.focus?.();
            status.textContent = S.photoDeleted;
        } catch (error) {
            if (error.status !== 401) alert.textContent = errorMessage(error);
        }
    }

    async function onUpload(file, caption) {
        clearMessages();
        const problem = checkFile(file, images.length);
        if (problem) {
            upload.showError(problem);
            return;
        }

        upload.setBusy(true);
        try {
            const image = await uploadPlaceImage(place.id, file, caption);
            images = [...images, image];
            upload.reset();
            render();
            status.textContent = S.photoUploaded;
        } catch (error) {
            if (error.status !== 401) upload.showError(errorMessage(error)); // e.g. not a real image (400) or storage down (502)
        } finally {
            upload.setBusy(false);
        }
    }

    function clearMessages() {
        status.textContent = '';
        alert.textContent = '';
        upload.showError(null);
    }
}

// The browser checks type, size and count first; the server checks the real file content again.
function checkFile(file, count) {
    if (!file) return S.errNoFile;
    if (count >= MAX_PHOTOS) return S.errPhotoLimit(MAX_PHOTOS);
    const typeOk = ALLOWED_TYPES.includes(file.type) || (!file.type && ALLOWED_EXTENSIONS.test(file.name));
    if (!typeOk) return S.errFileType;
    if (file.size > MAX_FILE_BYTES) return S.errFileSize((file.size / (1024 * 1024)).toFixed(1));
    return null;
}

function buildUploadForm(onUpload) {
    const fileInput = el('input', {
        id: 'photo-file', type: 'file', accept: ALLOWED_TYPES.join(','), 'aria-describedby': 'photo-file-error',
    });
    const captionInput = el('input', {
        id: 'photo-caption', type: 'text', maxlength: '200', autocomplete: 'off', 'aria-describedby': 'photo-caption-hint',
    });
    const error = el('p', { id: 'photo-file-error', className: 'field-error live-region', role: 'alert' });
    const button = el('button', { type: 'submit', className: 'button button-primary' }, [S.uploadPhoto]);
    const fullNote = el('p', { className: 'hint', hidden: true, text: S.errPhotoLimit(MAX_PHOTOS) });

    const element = el('form', { className: 'photo-upload', novalidate: true }, [
        el('div', { className: 'field-row' }, [
            el('div', { className: 'field' }, [el('label', { for: 'photo-file', text: S.photoFile }), fileInput]),
            el('div', { className: 'field' }, [
                el('label', { for: 'photo-caption', text: S.photoCaption }),
                captionInput,
                el('p', { id: 'photo-caption-hint', className: 'hint', text: S.photoCaptionHint }),
            ]),
        ]),
        error,
        fullNote,
        el('div', { className: 'form-actions' }, [button]),
    ]);

    element.addEventListener('submit', (event) => {
        event.preventDefault();
        onUpload(fileInput.files?.[0] ?? null, captionInput.value.trim());
    });

    let busy = false;
    let full = false;
    const updateDisabled = () => {
        fileInput.disabled = full;
        captionInput.disabled = full;
        button.disabled = busy || full;
        button.textContent = busy ? S.uploading : S.uploadPhoto;
        fullNote.hidden = !full;
    };

    return {
        element,
        showError(message) {
            error.textContent = message ?? '';
            if (message) fileInput.setAttribute('aria-invalid', 'true');
            else fileInput.removeAttribute('aria-invalid');
        },
        setBusy(value) {
            busy = value;
            updateDisabled();
        },
        setFull(value) {
            full = value;
            updateDisabled();
        },
        reset() {
            element.reset();
        },
    };
}
