(function () {
    'use strict';

    const config = document.getElementById('oaConfig');
    const urls = {
        confirm: config.dataset.confirmUrl,
        cancel: config.dataset.cancelUrl,
        paid: config.dataset.paidUrl,
        notify: config.dataset.notifyUrl
    };

    function getToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    // ===== TOAST =====
    window.showToast = function (message, type) {
        type = type || 'success';
        const icons = { success: 'ti-circle-check', error: 'ti-alert-circle', info: 'ti-info-circle' };
        const container = document.getElementById('oaToastContainer');

        const toast = document.createElement('div');
        toast.className = 'oa-toast oa-toast-' + type;

        const icon = document.createElement('i');
        icon.className = 'ti ' + (icons[type] || icons.info);

        const text = document.createElement('span');
        text.textContent = message;

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'oa-toast-close';
        close.innerHTML = '&times;';

        toast.appendChild(icon);
        toast.appendChild(text);
        toast.appendChild(close);
        container.appendChild(toast);

        requestAnimationFrame(function () { toast.classList.add('oa-toast-show'); });

        function dismiss() {
            toast.classList.remove('oa-toast-show');
            setTimeout(function () { toast.remove(); }, 300);
        }
        close.onclick = dismiss;
        setTimeout(dismiss, 4000);
    };

    // ===== GENERIC POST (para sa mga JSON actions) =====
    async function postAction(url, data, btn, reloadOnSuccess) {
        if (btn) btn.disabled = true;

        try {
            const res = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': getToken()
                },
                body: new URLSearchParams(data).toString()
            });

            if (!res.ok) throw new Error('Request failed: ' + res.status);

            const result = await res.json();
            showToast(result.message, result.success ? 'success' : 'error');

            if (result.success && reloadOnSuccess) {
                setTimeout(function () { location.reload(); }, 1300);
                return result; // naka-disable pa rin ang button hanggang mag-reload
            }

            if (btn) btn.disabled = false;
            return result;
        } catch (e) {
            showToast('Something went wrong. Please try again.', 'error');
            if (btn) btn.disabled = false;
        }
    }

    // ===== CONFIRM =====
    window.confirmOrder = function (orderId, btn) {
        if (!confirm('Confirm this order? Stock will be deducted from inventory.')) return;
        postAction(urls.confirm, { orderId: orderId }, btn, true);
    };

    // ===== CANCEL (modal) =====
    window.openCancelModal = function () {
        document.getElementById('oaCancelReason').value = '';
        document.getElementById('oaCancelError').classList.add('d-none');
        bootstrap.Modal.getOrCreateInstance(document.getElementById('oaCancelModal')).show();
    };

    window.submitCancelOrder = async function (orderId) {
        const reasonEl = document.getElementById('oaCancelReason');
        const errorEl = document.getElementById('oaCancelError');
        const submitBtn = document.getElementById('oaCancelSubmit');
        const reason = reasonEl.value.trim();

        if (!reason) {
            errorEl.textContent = 'Please enter a cancellation reason.';
            errorEl.classList.remove('d-none');
            return;
        }
        errorEl.classList.add('d-none');

        const result = await postAction(urls.cancel, { orderId: orderId, reason: reason }, submitBtn, true);

        if (result && result.success) {
            bootstrap.Modal.getOrCreateInstance(document.getElementById('oaCancelModal')).hide();
        }
    };

    // ===== MARK AS PAID =====
    window.markAsPaid = function (orderId, btn) {
        if (!confirm('Mark this COD order as paid? The order will be set to Completed.')) return;
        postAction(urls.paid, { orderId: orderId }, btn, true);
    };

    // ===== NOTIFY CUSTOMER =====
    window.notifyCustomer = function (orderId, btn) {
        postAction(urls.notify, { orderId: orderId }, btn, false);
    };
})();