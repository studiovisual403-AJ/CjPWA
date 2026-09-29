function displayFileName(file) {
    if (!file) return;

    const fileNameDisplay = document.getElementById('file-name-display');
    const imagePreview = document.getElementById('image-preview');
    const uploadPlaceholder = document.getElementById('upload-placeholder');
    const imageActions = document.getElementById('image-actions');

    if (fileNameDisplay) {
        fileNameDisplay.innerHTML = '✔ ' + file.name;
    }

    const reader = new FileReader();
    reader.onload = function (e) {
        if (imagePreview) {
            imagePreview.src = e.target.result;
            imagePreview.style.display = 'block';
        }
        if (uploadPlaceholder) {
            uploadPlaceholder.style.display = 'none';
        }
        if (imageActions) {
            imageActions.style.display = 'flex';
        }
    };

    reader.readAsDataURL(file);
}

function removeImage(e) {
    e.stopPropagation();

    const input = document.getElementById('ProductImage');
    const imagePreview = document.getElementById('image-preview');
    const uploadPlaceholder = document.getElementById('upload-placeholder');
    const imageActions = document.getElementById('image-actions');
    const fileNameDisplay = document.getElementById('file-name-display');

    if (input) {
        input.value = '';
    }
    if (imagePreview) {
        imagePreview.style.display = 'none';
    }
    if (uploadPlaceholder) {
        uploadPlaceholder.style.display = 'block';
    }
    if (imageActions) {
        imageActions.style.display = 'none';
    }
    if (fileNameDisplay) {
        fileNameDisplay.innerHTML = '';
    }
}

function changeImage(e) {
    e.stopPropagation();
    const input = document.getElementById('ProductImage');
    if (input) {
        input.click();
    }
}

function handleFileDrop(event) {
    event.preventDefault();

    const file = event.dataTransfer.files[0];
    if (!file) return;

    const input = document.getElementById('ProductImage');
    if (input) {
        input.files = event.dataTransfer.files;
    }

    displayFileName(file);
}

window.displayFileName = displayFileName;
window.removeImage = removeImage;
window.changeImage = changeImage;
window.handleFileDrop = handleFileDrop;
