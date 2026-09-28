if (localStorage.getItem('token') && localStorage.getItem('isAdmin') === 'true') {
    alert('Access denied. Customers only.');
    window.location.href = 'admin.html';
}

if (!localStorage.getItem('token')) {
    window.location.href = 'auth.html';
}

document.getElementById('user-greeting').innerText = `Welcome, ${localStorage.getItem('name') || 'User'}`;

document.getElementById('logout-btn').addEventListener('click', () => {
    localStorage.clear();
    window.location.href = 'auth.html';
});

const token = localStorage.getItem('token');
const roomsContainer = document.getElementById('rooms-list-container');

async function loadRooms() {
    try {
        const response = await fetch('/api/customer/rooms', {
            method: 'GET',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (!response.ok) throw new Error('Failed to load rooms');
        const rooms = await response.json();

        if (rooms.length === 0) {
            roomsContainer.innerHTML = '<div class="text-center text-muted small py-3">No rooms available at the moment.</div>';
            return;
        }

        roomsContainer.innerHTML = '';
        
        rooms.forEach(room => {
            const card = document.createElement('div');
            card.className = 'border rounded p-3 bg-white text-start shadow-sm hour-btn'; 
            card.style.cursor = 'pointer';
            
            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <h3 class="h6 fw-bold text-dark mb-1">${room.title}</h3>
                        <span class="badge bg-light text-secondary border">Room № ${room.number}</span>
                    </div>
                    <span class="badge text-white px-2 py-1 small btn-olive">Capacity: ${room.capacity}</span>
                </div>
            `;

            card.addEventListener('click', () => {
                window.location.href = `room-details.html?id=${room.id}&title=${encodeURIComponent(room.title)}&number=${encodeURIComponent(room.number)}`;
            });

            roomsContainer.appendChild(card);
        });

    } catch (error) {
        roomsContainer.innerHTML = `<div class="text-center text-danger small py-3">Error: ${error.message}</div>`;
    }
}

window.onload = loadRooms;

const logoutBtn = document.getElementById('logout-btn');
if (logoutBtn) {
    logoutBtn.addEventListener('click', () => {
        localStorage.clear(); 
        window.location.href = 'auth.html'; 
    });
}