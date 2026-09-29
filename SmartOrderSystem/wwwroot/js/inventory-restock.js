function initRestockModal() {
    const restockModal = document.getElementById('restockModal');

    if (!restockModal) {
        return;
    }

    restockModal.addEventListener('show.bs.modal', function (event) {
        const button = event.relatedTarget;
        const productName = button ? button.getAttribute('data-product-name') : '';
        const productSku = button ? button.getAttribute('data-product-sku') : '';

        const nameElement = document.getElementById('modalProductName');
        const skuElement = document.getElementById('modalProductSku');
        const hiddenSkuInput = document.getElementById('hiddenProductSku');

        if (nameElement) {
            nameElement.textContent = productName;
        }

        if (skuElement) {
            skuElement.textContent = productSku;
        }

        if (hiddenSkuInput) {
            hiddenSkuInput.value = productSku;
        }
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initRestockModal);
} else {
    initRestockModal();
}

window.initRestockModal = initRestockModal;
