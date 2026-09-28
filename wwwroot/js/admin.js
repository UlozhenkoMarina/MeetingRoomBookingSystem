if (localStorage.getItem('isAdmin') !== 'true') {
    alert('Access denied. Administrators only.');
    window.location.href = 'auth.html';
}

document.getElementById('admin-greeting').innerText = `Welcome, ${localStorage.getItem('name') || 'Admin'}`;

document.getElementById('logout-btn').addEventListener('click', () => {
    localStorage.clear();
    window.location.href = 'auth.html';
});

const slotsDictionary = new Map();

const hoursGrid = document.getElementById('hours-grid');
for (let h = 0; h < 24; h++) {
    const padHour = String(h).padStart(2, '0');
    const col = document.createElement('div');
    col.className = 'col-3';
    col.innerHTML = `<div class="hour-btn" data-hour="${h}">${padHour}:00</div>`;
    hoursGrid.appendChild(col);
}

hoursGrid.addEventListener('click', (e) => {
    if (e.target.classList.contains('hour-btn')) {
        e.target.classList.toggle('active');
    }
});

function calculateCurrentBitmask() {
    let mask = 0;
    const activeButtons = hoursGrid.querySelectorAll('.hour-btn.active');
    activeButtons.forEach(btn => {
        const hour = parseInt(btn.getAttribute('data-hour'));
        mask |= (1 << hour);
    });
    return mask;
}

function resetHourButtons() {
    hoursGrid.querySelectorAll('.hour-btn').forEach(btn => btn.classList.remove('active'));
}

document.getElementById('add-date-list-btn').addEventListener('click', () => {
    const dateInput = document.getElementById('slot-date').value;
    if (!dateInput) {
        alert('Please pick a specific date first!');
        return;
    }

    const currentMask = calculateCurrentBitmask();
    if (currentMask === 0) {
        alert('Please select at least one hour slot for this date!');
        return;
    }

    slotsDictionary.set(dateInput, currentMask);
    renderBatchPreview();
    resetHourButtons();
    document.getElementById('slot-date').value = '';
});

function renderBatchPreview() {
    const container = document.getElementById('batch-preview-container');
    const list = document.getElementById('batch-list');
    list.innerHTML = '';

    if (slotsDictionary.size > 0) {
        container.classList.remove('d-none');
    } else {
        container.classList.add('d-none');
    }

    slotsDictionary.forEach((mask, date) => {
        let hourCount = 0;
        for (let i = 0; i < 24; i++) { if ((mask & (1 << i)) !== 0) hourCount++; }

        list.innerHTML += `
            <li class="list-group-item d-flex justify-content-between align-items-center small py-2">
                <span>📅 <strong>${date}</strong> — ${hourCount} slot(s) configured</span>
                <button type="button" class="btn btn-sm text-danger p-0 border-0 fw-bold" onclick="removeDateFromBatch('${date}')">Remove</button>
            </li>
        `;
    });
}

window.removeDateFromBatch = function(dateKey) {
    slotsDictionary.delete(dateKey);
    renderBatchPreview();
};

document.getElementById('room-master-form').addEventListener('submit', async (e) => {
    e.preventDefault();

    if (slotsDictionary.size === 0) {
        alert('Your batch queue is empty! Configure at least one date with slots.');
        return;
    }

    const roomTitle = document.getElementById('room-title').value;
    const roomNumber = document.getElementById('room-number').value; 
    const roomCapacity = parseInt(document.getElementById('room-capacity').value);
    const token = localStorage.getItem('token');

    const slotsObject = {};
    slotsDictionary.forEach((mask, date) => {
        slotsObject[`${date}T00:00:00Z`] = mask;
    });

  
    const bodyPayload = {
        title: roomTitle,
        number: roomNumber,
        capacity: roomCapacity,
        slotsPerDate: slotsObject 
    };

    try {
        const response = await fetch('/api/admin/bookings', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(bodyPayload)
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.message || 'Failed to save transaction.');
        }

        alert('Success: ' + data.message);
        
        document.getElementById('room-master-form').reset();
        slotsDictionary.clear();
        renderBatchPreview();

    } catch (err) {
        alert('Execution Error: ' + err.message);
    }
});
