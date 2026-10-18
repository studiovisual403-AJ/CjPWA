(() => {
    "use strict";

    const root = document.querySelector(".nb-root");
    if (!root) return;

    const badge = root.querySelector("[data-nb-badge]");
    const attentionCount = root.querySelector(".nb-attention-count");
    const menu = root.querySelector(".nb-menu");
    const urls = {
        order: root.dataset.orderDetailsUrl,
        reservation: root.dataset.reservationDetailsUrl,
        reorder: root.dataset.reorderUrl
    };
    let timer = null;

    const setText = (element, text) => {
        element.textContent = text;
    };

    const replaceItems = (section, entries, createItem) => {
        const list = root.querySelector(`[data-nb-list="${section}"]`);
        if (!list) return;

        list.replaceChildren(...entries.map(createItem));

        let empty = root.querySelector(`[data-nb-empty="${section}"]`);
        if (entries.length === 0) {
            if (!empty) {
                empty = document.createElement("p");
                empty.className = "nb-empty";
                empty.dataset.nbEmpty = section;
                list.after(empty);
            }
            setText(empty, {
                orders: "No pending orders.",
                reservations: "No pending reservations.",
                cancellations: "No recent cancellations.",
                rop: "No low stock alerts."
            }[section]);
        } else if (empty) {
            empty.remove();
        }
    };

    const makeEntry = (href, title, detail, overdue) => {
        const item = document.createElement("li");
        item.className = overdue ? "nb-item nb-overdue" : "nb-item";
        const link = document.createElement("a");
        link.href = href;
        const titleText = document.createElement("span");
        titleText.textContent = title;
        const detailText = document.createElement("small");
        detailText.textContent = detail;
        link.append(titleText, detailText);
        item.append(link);
        return item;
    };

    const detailsUrl = (template, id) => template.replace("__id__", encodeURIComponent(id));
    const ageText = (hours) => `${Math.floor(hours)}h ago`;

    const render = (summary) => {
        const count = summary.pendingOrderCount + summary.pendingReservationCount + summary.ropAlertCount;
        badge.hidden = count === 0;
        setText(badge, count > 99 ? "99+" : String(count));
        setText(attentionCount, `${count} need attention`);

        const nothing = root.querySelector(".nb-nothing");
        if (count === 0) {
            if (!nothing) {
                const message = document.createElement("p");
                message.className = "nb-nothing";
                message.textContent = "Nothing needs attention.";
                menu.querySelector(".nb-menu-head").after(message);
            }
        } else if (nothing) {
            nothing.remove();
        }

        root.querySelector('[aria-label="Pending orders"] .nb-count').textContent = summary.pendingOrderCount;
        root.querySelector('[aria-label="Pending reservations"] .nb-count').textContent = summary.pendingReservationCount;
        root.querySelector('[aria-label="Recent cancellations"] .nb-count').textContent = summary.cancellationCount;
        root.querySelector('[aria-label="Low stock alerts"] .nb-count').textContent = summary.ropAlertCount;

        replaceItems("orders", summary.pendingOrders, item =>
            makeEntry(
                detailsUrl(urls.order, item.orderId),
                `Order #${item.orderId} · ${item.customerName}`,
                `Total ${Number(item.total).toFixed(2)} · ${ageText(item.ageHours)}${item.isOverdue ? " · OVERDUE" : ""}`,
                item.isOverdue
            ));
        replaceItems("reservations", summary.pendingReservations, item =>
            makeEntry(
                detailsUrl(urls.reservation, item.reservationId),
                `Reservation #${item.reservationId} · ${item.customerName}`,
                `Qty ${item.quantity} · ${ageText(item.ageHours)}${item.isOverdue ? " · OVERDUE" : ""}`,
                item.isOverdue
            ));
        replaceItems("cancellations", summary.recentCancellations, item =>
            makeEntry(
                detailsUrl(urls.order, item.orderId),
                `Order #${item.orderId} · ${item.cancellationReason || "No reason recorded"}`,
                new Date(item.updatedAt).toLocaleString(),
                false
            ));
        replaceItems("rop", summary.ropAlerts, item =>
            makeEntry(
                `${urls.reorder}?status=${encodeURIComponent(item.status)}`,
                `${item.modelName} · Size ${Number(item.size).toLocaleString()}`,
                `${item.status} · Stock ${item.stock} / ROP ${item.reorderPoint}`,
                item.status === "Out of Stock"
            ));
    };

    const poll = async () => {
        timer = null;
        if (document.hidden) return;

        try {
            const response = await fetch(root.dataset.summaryUrl, {
                headers: { Accept: "application/json" },
                cache: "no-store",
                credentials: "same-origin"
            });
            if (!response.ok) throw new Error(`Alert summary returned HTTP ${response.status}`);
            render(await response.json());
        } catch (error) {
            console.error("Unable to refresh admin alerts.", error);
        } finally {
            schedule();
        }
    };

    const schedule = () => {
        if (!document.hidden && timer === null) {
            timer = window.setTimeout(poll, 60000);
        }
    };

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            if (timer !== null) {
                window.clearTimeout(timer);
                timer = null;
            }
        } else {
            schedule();
        }
    });

    schedule();
})();
