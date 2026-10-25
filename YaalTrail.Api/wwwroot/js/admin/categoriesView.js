// Categories screen (FR-46): list, create, rename and delete.
// The server refuses to delete a category that still has places; its message is shown.
import { el } from '../dom.js';
import { ADMIN_STRINGS as S } from './adminStrings.js';
import { createCategory, deleteCategory, getCategories, updateCategory } from './adminApi.js';
import { confirmAction } from './confirmDialog.js';
import { errorMessage } from './adminFormat.js';

// ctx = { dialog, showMessage(text, kind) }
export async function showCategoriesScreen(container, ctx) {
    let categories = [];
    let renamingId = null;

    const heading = el('h1', { className: 'screen-title', tabindex: '-1', text: S.categoriesTitle });
    const listArea = el('div', { className: 'table-area' }, [el('p', { className: 'muted', text: S.loading })]);
    const createForm = buildCreateForm(onCreate);

    container.replaceChildren(heading, createForm.element, listArea);
    heading.focus();
    await load();

    async function load() {
        try {
            categories = await getCategories();
            render();
        } catch (error) {
            if (error.status === 401) return;
            listArea.replaceChildren(el('div', { className: 'message message-error', role: 'alert' }, [
                el('p', { text: errorMessage(error) }),
                el('button', { type: 'button', className: 'button button-secondary', onClick: load }, [S.retry]),
            ]));
        }
    }

    function render() {
        if (categories.length === 0) {
            listArea.replaceChildren(el('p', { className: 'empty', text: S.noCategories }));
            return;
        }

        const headings = [S.categoryName, S.categoryDescription, S.colPlaces, S.colActions];
        listArea.replaceChildren(el('table', { className: 'data-table' }, [
            el('caption', { className: 'visually-hidden', text: S.categoriesTitle }),
            el('thead', {}, [el('tr', {}, headings.map((text) => el('th', { scope: 'col', text })))]),
            el('tbody', {}, categories.map((category) => (category.id === renamingId
                ? createRenameRow(category)
                : createRow(category)))),
        ]));
    }

    function createRow(category) {
        return el('tr', { 'data-row': String(category.id) }, [
            el('th', { scope: 'row', 'data-label': S.categoryName, text: category.name }),
            el('td', { 'data-label': S.categoryDescription, text: category.description ?? '' }),
            el('td', { 'data-label': S.colPlaces, text: String(category.placeCount) }),
            el('td', { 'data-label': S.colActions }, [el('div', { className: 'row-actions' }, [
                el('button', {
                    type: 'button',
                    className: 'button button-secondary button-small',
                    'data-action': 'rename',
                    'aria-label': S.renameCategory(category.name),
                    onClick: () => startRename(category.id),
                }, [S.rename]),
                el('button', {
                    type: 'button',
                    className: 'button button-danger-outline button-small',
                    'data-action': 'delete',
                    'aria-label': S.deleteCategory(category.name),
                    onClick: () => onDelete(category),
                }, [S.delete]),
            ])]),
        ]);
    }

    // Rename happens in place: the name cell becomes a small form.
    function createRenameRow(category) {
        const inputId = `rename-${category.id}`;
        const error = el('p', { id: `${inputId}-error`, className: 'field-error', hidden: true });
        const input = el('input', { id: inputId, type: 'text', value: category.name, maxlength: '100', required: true });
        const form = el('form', { className: 'rename-form', novalidate: true }, [
            el('label', { for: inputId, className: 'visually-hidden', text: S.newName(category.name) }),
            input,
            el('button', { type: 'submit', className: 'button button-primary button-small' }, [S.save]),
            el('button', { type: 'button', className: 'button button-secondary button-small', onClick: cancelRename }, [S.cancel]),
            error,
        ]);

        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            await onRename(category, input, error);
        });
        input.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') cancelRename();
        });

        return el('tr', { 'data-row': String(category.id) }, [
            el('td', { colspan: '4' }, [form]),
        ]);
    }

    function startRename(id) {
        renamingId = id;
        render();
        const input = document.getElementById(`rename-${id}`);
        input?.focus();
        input?.select();
    }

    function cancelRename() {
        const id = renamingId;
        renamingId = null;
        render();
        focusAction(id, 'rename');
    }

    // FR-46: PUT sends the whole category, so the description and icon are kept.
    async function onRename(category, input, error) {
        const name = input.value.trim();
        if (!name) {
            showInputError(input, error, S.errNameRequired);
            return;
        }

        try {
            const saved = await updateCategory(category.id, {
                name,
                description: category.description,
                iconRef: category.iconRef,
            });
            renamingId = null;
            ctx.showMessage(S.categoryRenamed(saved.name));
            await load();
            focusAction(category.id, 'rename');
        } catch (err) {
            if (err.status === 401) return;
            showInputError(input, error, errorMessage(err)); // e.g. duplicate name
        }
    }

    async function onCreate({ name, description }, fields) {
        try {
            const saved = await createCategory({ name, description: description || null, iconRef: null });
            fields.reset();
            ctx.showMessage(S.categoryCreated(saved.name));
            await load();
        } catch (err) {
            if (err.status === 401) return;
            fields.showNameError(errorMessage(err));
        }
    }

    // FR-46: the server answers 409 with a reason if places still use the category.
    async function onDelete(category) {
        const confirmed = await confirmAction(ctx.dialog, {
            title: S.deleteCategoryTitle(category.name),
            lines: [category.placeCount > 0 ? S.deleteCategoryInUse(category.placeCount) : null, S.deleteCannotUndo],
            confirmLabel: S.deleteCategoryConfirm,
        });
        if (!confirmed) return;

        try {
            await deleteCategory(category.id);
            ctx.showMessage(S.categoryDeleted(category.name));
            await load();
            heading.focus();
        } catch (err) {
            if (err.status === 401) return;
            ctx.showMessage(errorMessage(err), 'error');
            if (err.status === 404) await load();
        }
    }

    function focusAction(id, action) {
        listArea.querySelector(`[data-row="${id}"] [data-action="${action}"]`)?.focus();
    }
}

function buildCreateForm(onCreate) {
    const nameInput = el('input', { id: 'new-category-name', type: 'text', maxlength: '100', required: true, autocomplete: 'off' });
    const descriptionInput = el('input', { id: 'new-category-description', type: 'text', maxlength: '500', autocomplete: 'off' });
    const nameError = el('p', { id: 'new-category-name-error', className: 'field-error', hidden: true });

    const fields = {
        reset() {
            nameInput.value = '';
            descriptionInput.value = '';
            showInputError(nameInput, nameError, null);
        },
        showNameError(message) {
            showInputError(nameInput, nameError, message);
            nameInput.focus();
        },
    };

    const element = el('form', { className: 'inline-form', novalidate: true }, [
        el('h2', { className: 'section-title', text: S.addCategory }),
        el('div', { className: 'field-row' }, [
            el('div', { className: 'field' }, [
                el('label', { for: 'new-category-name' }, [S.categoryName, ' ', el('span', { className: 'required', text: S.required })]),
                nameInput,
                nameError,
            ]),
            el('div', { className: 'field' }, [
                el('label', { for: 'new-category-description', text: S.categoryDescription }),
                descriptionInput,
            ]),
        ]),
        el('div', { className: 'form-actions' }, [
            el('button', { type: 'submit', className: 'button button-primary' }, [S.addCategory]),
        ]),
    ]);

    element.addEventListener('submit', async (event) => {
        event.preventDefault();
        const name = nameInput.value.trim();
        if (!name) {
            fields.showNameError(S.errNameRequired);
            return;
        }
        showInputError(nameInput, nameError, null);
        await onCreate({ name, description: descriptionInput.value.trim() }, fields);
    });

    return { element };
}

function showInputError(input, slot, message) {
    slot.hidden = !message;
    slot.textContent = message ?? '';
    if (message) {
        input.setAttribute('aria-invalid', 'true');
        input.setAttribute('aria-describedby', slot.id);
    } else {
        input.removeAttribute('aria-invalid');
        input.removeAttribute('aria-describedby');
    }
}
