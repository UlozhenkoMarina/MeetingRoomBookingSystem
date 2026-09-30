let signalRConnection = null; 

if (!localStorage.getItem('token')) {
    window.location.href = 'auth.html';
}

document.getElementById('user-greeting').innerText = `Welcome, ${localStorage.getItem('name') || 'User'}`;

document.getElementById('back-btn').addEventListener('click', () => {
    window.location.href = 'customer_overview.html';
});

const token = localStorage.getItem('token');

const storedRoom = localStorage.getItem('selectedRoomDTO');
if (!storedRoom) {
    alert("Room data is missing! Returning to overview.");
    window.location.href = 'customer_overview.html';
}

const inputRoomDTO = JSON.parse(storedRoom);
const roomId = inputRoomDTO.id; 

const bookingDate = document.getElementById('booking-date');
const slotsBlock = document.getElementById('slots-block');
const hoursGrid = document.getElementById('customer-hours-grid');
const bookBtn = document.getElementById('book-btn');

let cachedBookingList = [];

async function initRoomAndSchedulePage() {
    const date = bookingDate.value;
    const targetDate = date || new Date().toISOString().split('T')[0]; 
    if (!date) bookingDate.value = targetDate;

    try {
        const jsonString = JSON.stringify(inputRoomDTO);
        
        const response = await fetch(`/api/customer/booking?jsonDto=${encodeURIComponent(jsonString)}&date=${targetDate}`, {
            method: 'GET',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (!response.ok) throw new Error('Failed to load room schedule details');
        const data = await response.json();

        document.getElementById('room-title-display').innerText = data.room.title;
        document.getElementById('room-number-display').innerText = `Room № ${data.room.number} (Capacity: ${data.room.capacity})`;

        cachedBookingList = data.bookingList || [];
        renderHourlySchedule();

        if (!signalRConnection) {
            startSignalRConnection();
        }
    } catch (error) {
        console.error('Error initializing room page:', error);
        document.getElementById('room-title-display').innerText = "Error loading room data";
    }
}

function renderHourlySchedule() {
    let finalDayBookedMask = 0;
    cachedBookingList.forEach(record => {
        const mask = record.bookedSlots !== undefined ? record.bookedSlots : record.BookedSlots;
        if (mask !== undefined) {
            finalDayBookedMask |= mask; 
        }
    });

    hoursGrid.innerHTML = '';
    slotsBlock.classList.remove('d-none');
    bookBtn.removeAttribute('disabled');

    for (let h = 0; h < 24; h++) {
        const padHour = String(h).padStart(2, '0');
        const isOccupied = (finalDayBookedMask & (1 << h)) !== 0; 

        const col = document.createElement('div');
        col.className = 'col-3';

        if (isOccupied) {
            col.innerHTML = `<div class="hour-btn style-disabled">${padHour}:00</div>`;
        } else {
            col.innerHTML = `<div class="hour-btn" data-hour="${h}">${padHour}:00</div>`;
        }
        hoursGrid.appendChild(col);
    }
}

bookingDate.addEventListener('change', initRoomAndSchedulePage);
window.onload = initRoomAndSchedulePage;

hoursGrid.addEventListener('click', (e) => {
    if (e.target.classList.contains('hour-btn') && !e.target.classList.contains('style-disabled')) {
        e.target.classList.toggle('active');
    }
});

document.getElementById('booking-form').addEventListener('submit', async (e) => {
    e.preventDefault();

    let currentMask = 0;
    hoursGrid.querySelectorAll('.hour-btn.active').forEach(btn => {
        currentMask |= (1 << parseInt(btn.getAttribute('data-hour')));
    });

    if (currentMask === 0) {
        alert('Please select at least one available hour slot!');
        return;
    }

    const selectedDate = bookingDate.value;
    const slotsObject = {};
    slotsObject[`${selectedDate}T00:00:00Z`] = currentMask;

    const payload = {
        roomId: parseInt(roomId),
        bookingDate: `${selectedDate}T00:00:00Z`,
        slotsPerDate: slotsObject
    };

    try {
        const response = await fetch('/api/customer/bookings', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        const result = await response.json();
        if (!response.ok) throw new Error(result.message || 'Booking failed.');

        alert('Success: ' + result.message);
        initRoomAndSchedulePage(); 
    } catch (error) {
        alert('Error: ' + error.message);
    }
});

async function startSignalRConnection() {
    if (typeof signalR === 'undefined') {
        console.warn(" SignalR is not available yet");
        setTimeout(startSignalRConnection, 1000); 
        return;
    }

    try {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/bookingHub")
            .withAutomaticReconnect() 
            .build();

        signalRConnection.on("ReceiveBookingUpdate", (updatedRoomId, updatedDate) => {
            if (parseInt(updatedRoomId) === parseInt(roomId) && updatedDate === bookingDate.value) {
                console.log(" SignalR: New data");
                initRoomAndSchedulePage(); 
            }
        });

        await signalRConnection.start();
        console.log(" SignalR successful connection");
    } catch (err) {
        console.error("Connection to SignalR Hub error: ", err);
        signalRConnection = null; 
        setTimeout(startSignalRConnection, 5000);
    }
}
