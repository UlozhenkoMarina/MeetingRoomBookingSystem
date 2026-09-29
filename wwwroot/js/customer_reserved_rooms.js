if (!localStorage.getItem('token')) {
    window.location.href = 'auth.html';
}

document.getElementById('user-greeting').innerText = `Welcome, ${localStorage.getItem('name') || 'User'}`;

document.getElementById('back-btn').addEventListener('click', () => {
    window.location.href = 'customer_overview.html';
});

const token = localStorage.getItem('token');
const historyContainer = document.getElementById('bookings-history-list');

async function loadMyBookings() {
    try {
        const response = await fetch('/api/customer/reserved_rooms', {
            method: 'GET',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (!response.ok) throw new Error('Failed to load reservation history.');
        const bookings = await response.json();

        if (bookings.length === 0) {
            historyContainer.innerHTML = '<div class="text-center text-muted small py-4">You have not made any bookings yet.</div>';
            return;
        }

        historyContainer.innerHTML = '';

        bookings.forEach(item => {
            const record = item.record;
            const activeHours = [];
            
            for (let h = 0; h < 24; h++) {
                if ((record.bookedSlots & (1 << h)) !== 0) {
                    activeHours.push(`${String(h).padStart(2, '0')}:00`);
                }
            }

            const card = document.createElement('div');
            card.className = 'border rounded p-3 bg-white shadow-sm text-start';
            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <div>
                        <h3 class="h6 fw-bold text-dark mb-1">${item.roomTitle}</h3>
                        <span class="badge bg-light text-secondary border">Room  ${item.roomNumber}</span>
                    </div>
                    <span class="badge btn-olive text-white px-3 py-2 fs-7 fw-semibold shadow-sm">${item.bookingDate}</span>
                </div>
                <div class="pt-3 border-top mt-2">
                    <span class="text-secondary small fw-medium">Your Reserved Slots:</span>
                    <div class="d-flex flex-wrap gap-1 mt-2">
                        ${activeHours.map(hr => `<span class="badge bg-light text-dark border px-2 py-1.5 fs-7 fw-normal">${hr}</span>`).join('')}
                    </div>
                </div>
            `;
            historyContainer.appendChild(card);
        });

    } catch (error) {
        historyContainer.innerHTML = `<div class="text-center text-danger small py-3">Error: ${error.message}</div>`;
    }
}

window.onload = loadMyBookings;
