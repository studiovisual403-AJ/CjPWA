function initProductImageUpload() {
    const uploadModal = document.getElementById('miUploadModal');

    // 1. Modal Elements (Tamang modal IDs)
    const dropzoneModal = document.getElementById('miDropzoneModal');
    const fileInputModal = document.getElementById('miFileInputModal');
    const filePreviewModal = document.getElementById('miFilePreviewModal');

    // 2. Inline Dropzone Elements
    const dropzoneInline = document.getElementById('miDropzoneInline');
    const fileInputInline = document.getElementById('miFileInputInline');

    function openUploadModal() {
        if (uploadModal) {
            uploadModal.style.display = 'flex';
        }
    }

    function closeUploadModal() {
        if (!uploadModal) return;
        uploadModal.style.display = 'none';
        if (fileInputModal) fileInputModal.value = '';
        if (filePreviewModal) filePreviewModal.innerHTML = '';
    }

    function showPreview(inputElement) {
        if (!inputElement || !inputElement.files || inputElement.files.length === 0) return;

        const file = inputElement.files[0];
        const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
        const maxSize = 5 * 1024 * 1024;

        if (!allowedTypes.includes(file.type)) {
            alert(`${file.name} is not a supported image format.`);
            inputElement.value = '';
            return;
        }
        if (file.size > maxSize) {
            alert(`${file.name} exceeds the maximum size of 5MB.`);
            inputElement.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = function (e) {
            const dropzone = inputElement.closest('.mi-dropzone');
            const previewImg = dropzone ? dropzone.querySelector('#mi-image-preview') : null;
            const placeholder = dropzone ? dropzone.querySelector('#mi-upload-placeholder') : null;
            const fileNameDisplay = dropzone ? dropzone.querySelector('#mi-file-name-display') : null;
            const imageActions = dropzone ? dropzone.querySelector('#mi-image-actions') : null;

            if (previewImg) {
                previewImg.src = e.target.result;
                previewImg.style.display = 'block';
            }
            if (placeholder) {
                placeholder.style.display = 'none';
            }
            if (fileNameDisplay) {
                fileNameDisplay.textContent = '✔ ' + file.name;
                fileNameDisplay.style.display = 'block';
            }
            if (imageActions) {
                imageActions.style.display = 'flex';
            }
        };
        reader.readAsDataURL(file);
    }

    // --- SETUP PARA SA MODAL DROPZONE (Isang maayos na listener lang) ---
    if (dropzoneModal && fileInputModal) {
        dropzoneModal.addEventListener('click', function(e) {
            // Huwag buksan ang file explorer kung ang pinindot ay ang buttons o actions container
            if (e.target.closest('#mi-image-actions') || e.target.closest('button')) {
                return;
            }
            fileInputModal.click();
        });

        fileInputModal.addEventListener('click', (e) => e.stopPropagation());

        fileInputModal.addEventListener('change', function() {
            showPreview(fileInputModal);
        });
    }

    // --- SETUP PARA SA INLINE DROPZONE ---
    if (dropzoneInline && fileInputInline) {
        dropzoneInline.addEventListener('click', (e) => {
            if (!e.target.closest('label') && !e.target.closest('input')) {
                fileInputInline.click();
            }
        });

        fileInputInline.addEventListener('click', (e) => e.stopPropagation());

        fileInputInline.addEventListener('change', function () {
            if (fileInputInline.files.length > 0) {
                this.closest('form').submit();
            }
        });
    }

    // Modal Form Submit Validation
    const modalForm = document.querySelector('#miUploadModal form');
    if (modalForm && fileInputModal) {
        modalForm.addEventListener('submit', function (e) {
            if (fileInputModal.files.length === 0) {
                e.preventDefault();
                alert('Please select at least one image to upload.');
            }
        });
    }

    // --- OTHER ACTIONS (Set Cover, Delete, Replace, Rename) ---
    document.querySelectorAll('.mi-set-cover-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const imageId = this.dataset.imageId;
            const shoeId = this.dataset.shoeId;
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenInput ? tokenInput.value : '';

            fetch('/Product/SetAsCover', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `imageId=${imageId}&shoeId=${shoeId}&__RequestVerificationToken=${encodeURIComponent(token)}`
            }).then(res => {
                if (res.ok) location.reload();
                else alert('Unable to set the cover image.');
            });
        });
    });

    document.querySelectorAll('.mi-delete-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            if (!confirm('Delete this image? This cannot be undone.')) return;

            const imageId = this.dataset.imageId;
            const shoeId = this.dataset.shoeId;
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenInput ? tokenInput.value : '';

            fetch('/Product/DeleteImage', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: `imageId=${imageId}&shoeId=${shoeId}&__RequestVerificationToken=${encodeURIComponent(token)}`
            }).then(res => {
                if (res.ok) location.reload();
                else alert('Unable to delete the image.');
            });
        });
    });

    // ===============================
    // Replace Image
    // ===============================

    // Step 1: clicking the "Replace" button opens that image's hidden file input
    document.querySelectorAll('.replace-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const imageId = this.dataset.imageId;
            const input = document.getElementById(`replace-${imageId}`);
            if (input) {
                input.click();
            } else {
                console.warn(`Replace input not found for image ${imageId}`);
            }
        });
    });

    // Step 2: once a file is chosen, upload it via AJAX
    document.querySelectorAll('.mi-replace-input').forEach(input => {
         input.addEventListener('change', function () {
            if (this.files.length === 0) return;

            const file = this.files[0];
            const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
            const maxSize = 5 * 1024 * 1024;

            if (!allowedTypes.includes(file.type)) {
                alert(`${file.name} is not a supported image format.`);
                this.value = '';
                return;
            }
            if (file.size > maxSize) {
                alert(`${file.name} exceeds the maximum size of 5MB.`);
                this.value = '';
                return;
            }

            const imageId = this.dataset.imageId;

            const formData = new FormData();
            formData.append("imageId", imageId);
            formData.append("imageFile", file);

            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            if (tokenInput) {
                formData.append("__RequestVerificationToken", tokenInput.value);
            }

            fetch('/Product/ReplaceImage', {
                method: 'POST',
                body: formData
            })
            .then(res => {
                if (res.ok) {
                    location.reload();
                } else {
                    alert("Unable to replace the image.");
                }
            })
            .catch(() => alert("Something went wrong while replacing the image."));
        });
    });

    window.miOpenUploadModal = openUploadModal;
    window.miCloseUploadModal = closeUploadModal;
}

// --- GLOBAL BUTTON FUNCTIONS (Para maabot ng HTML onclick) ---
window.miChangeImage = function(event) {
    if (event) {
        event.preventDefault();
        event.stopPropagation();
    }
    const fileInput = document.getElementById('miFileInputModal');
    if (fileInput) fileInput.click();
};

window.miRemoveImage = function(event) {
    if (event) {
        event.preventDefault();
        event.stopPropagation();
        if (typeof event.stopImmediatePropagation === 'function') {
            event.stopImmediatePropagation();
        }
    }

    const fileInput = document.getElementById('miFileInputModal');
    const dropzone = fileInput ? fileInput.closest('.mi-dropzone, .drag-drop-zone') : null;

    if (fileInput) fileInput.value = ''; // I-clear ang file input

    const previewImg = dropzone ? dropzone.querySelector('#mi-image-preview') : null;
    const placeholder = dropzone ? dropzone.querySelector('#mi-upload-placeholder') : null;
    const fileNameDisplay = dropzone ? dropzone.querySelector('#mi-file-name-display') : null;
    const imageActions = dropzone ? dropzone.querySelector('#mi-image-actions') : null;

    if (previewImg) {
        previewImg.src = '';
        previewImg.style.display = 'none';
    }
    if (placeholder) {
        placeholder.style.display = 'block';
    }
    if (fileNameDisplay) {
        fileNameDisplay.innerHTML = '';
        fileNameDisplay.style.display = 'none';
    }
    if (imageActions) {
        imageActions.style.display = 'none'; // Mawawala ang buttons pag niremove, babalik pag nag-upload ulit
    }
};

window.saveFileName = function(imageId, newFileName) {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';

    fetch('/Product/UpdateImageName', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: `imageId=${imageId}&newFileName=${encodeURIComponent(newFileName)}&__RequestVerificationToken=${encodeURIComponent(token)}`
    }).then(res => res.json())
      .then(data => {
          if (!data.success) alert('Unable to update the filename.');
      });
};

// Initializer execution
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initProductImageUpload);
} else {
    initProductImageUpload();
}

const miImageListEl = document.getElementById('miImageList');
if (miImageListEl && typeof Sortable !== 'undefined') {
    new Sortable(miImageListEl, {
        animation: 150,
        handle: '.mi-drag-handle',
        onEnd: function () {
            const order = [];

            document.querySelectorAll('.mi-image-card-item').forEach(function (card, index) {
                order.push(Number(card.dataset.imageId));
            });

            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            fetch('/Product/ReorderImages', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({
                    shoeId: Number(miImageListEl.dataset.shoeId),
                    orderedImageIds: order
                })
            })
            .then(res => {
                if (res.ok) location.reload();
                else alert('Unable to save the image order.');
            });
        }
    });
}
