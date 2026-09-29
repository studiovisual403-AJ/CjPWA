function getCsrfToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value
        || document.querySelector('meta[name="csrf-token"]')?.content
        || '';
}

function showOrderFeedback(message, isSuccess) {
    const box = document.getElementById('oaFeedback');
    if (!box) {
        return;
    }

    box.className = `alert mt-3 ${isSuccess ? 'alert-success' : 'alert-danger'}`;
    box.textContent = message;
    box.classList.remove('d-none');
}

async function confirmOrder(orderId) {
    if (!confirm('Are you sure you want to confirm this order? Stock will be deducted.')) return;

    try {
        const res = await fetch('/OrdersManagement/ConfirmOrder', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `orderId=${orderId}&__RequestVerificationToken=${encodeURIComponent(getCsrfToken())}`
        });
        const data = await res.json();
        showOrderFeedback(data.message, data.success);
        if (data.success) setTimeout(() => location.reload(), 900);
    } catch (e) {
        showOrderFeedback('An error occurred while confirming the order.', false);
    }
}

function openCancelModal() {
    const cancelReason = document.getElementById('oaCancelReason');
    const cancelError = document.getElementById('oaCancelError');
    const cancelModal = document.getElementById('oaCancelModal');

    if (!cancelReason || !cancelError || !cancelModal) {
        return;
    }

    cancelReason.value = '';
    cancelError.classList.add('d-none');
    new bootstrap.Modal(cancelModal).show();
}

async function submitCancelOrder(orderId) {
    const reason = document.getElementById('oaCancelReason')?.value.trim() || '';
    const errBox = document.getElementById('oaCancelError');

    if (!reason) {
        if (errBox) {
            errBox.textContent = 'A cancellation reason is required before submitting.';
            errBox.classList.remove('d-none');
        }
        return;
    }

    try {
        const res = await fetch('/OrdersManagement/CancelOrder', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `orderId=${orderId}&reason=${encodeURIComponent(reason)}&__RequestVerificationToken=${encodeURIComponent(getCsrfToken())}`
        });
        const data = await res.json();
        if (data.success) {
            location.reload();
        } else if (errBox) {
            errBox.textContent = data.message;
            errBox.classList.remove('d-none');
        }
    } catch (e) {
        if (errBox) {
            errBox.textContent = 'An error occurred while cancelling the order.';
            errBox.classList.remove('d-none');
        }
    }
}

async function notifyCustomer(orderId) {
    try {
        const res = await fetch('/OrdersManagement/NotifyCustomer', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `orderId=${orderId}&__RequestVerificationToken=${encodeURIComponent(getCsrfToken())}`
        });
        const data = await res.json();
        showOrderFeedback(data.message, data.success);
    } catch (e) {
        showOrderFeedback('An error occurred while sending the notification.', false);
    }
}

async function markAsPaid(orderId) {
    if (!confirm('Are you sure you want to mark this order as paid and completed?')) return;

    try {
        const res = await fetch('/OrdersManagement/MarkAsPaid', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `orderId=${orderId}&__RequestVerificationToken=${encodeURIComponent(getCsrfToken())}`
        });
        const data = await res.json();
        showOrderFeedback(data.message, data.success);
        if (data.success) setTimeout(() => location.reload(), 900);
    } catch (e) {
        showOrderFeedback('An error occurred while marking the order as paid.', false);
    }
}

window.confirmOrder = confirmOrder;
window.openCancelModal = openCancelModal;
window.submitCancelOrder = submitCancelOrder;
window.notifyCustomer = notifyCustomer;
window.markAsPaid = markAsPaid;
