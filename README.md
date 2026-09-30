# 🌿 Meeting Room Booking System (Trainee Camp Project)

This project is a lightweight, high-performance web application designed for managing meeting room availability and processing real-time booking. It is built using **.NET 10 (Minimal APIs)**, **Entity Framework Core**, and **SignalR** for real-time frontend synchronization.

---

## 🚀 Key Engineering Solutions & System Functionality

### 1. High-Performance Schedule Compression (Bitmask Engine)
* **The Concept:** Standard reservation software often requires thousands of database rows to track hourly schedules for each room per day, leading to bloated database sizes and slow index lookups.
* **The Solution:** The entire 24-hour daily schedule is compressed into a single 32-bit integer (`int BookedSlots`). Each hour slot (00:00 to 23:00) corresponds to a bit position calculated via bitwise shifting (e.g., `1 << hour`).
* **Result:** Schedule aggregation, vacancy lookups, and overlapping time validations are performed via lightning-fast low-level bitwise operations (`AND`, `OR`). This reduces database storage size  and optimizes daily availability lookups.

### 2. Distributed Concurrency Control (Aggregate Root Pattern)
* **The Concept:** In high-traffic systems, multiple users might attempt to click and reserve the exact same hour slot simultaneously, creating a critical Race Condition (overbooking anomalies).
* **The Solution:** Implemented **Optimistic Concurrency Control** via the **Aggregate Root** pattern. The `Room` database model encapsulates the schedule state and acts as the structural gatekeeper, holding a dedicated concurrency token (`RoomVersion`).
* **Result:** Every time a slot changes, the system increments the entity version. If two parallel database threads read the same initial state and attempt concurrent updates, Entity Framework Core strictly rejects the second transaction, throwing a `DbUpdateConcurrencyException`. The second transaction rolls back safely, preventing data corruption.

### 3. Real-Time UI Updates (SignalR Infrastructure)
* **The Concept:** Forcing client browsers to poll the server every few seconds to refresh the schedule grid degrades backend performance and creates a lagging user experience.
* **The Solution:** Integrated an isolated server-to-client messaging layer using a SignalR Hub (`/bookingHub`).
* **Result:** Whenever a user successfully commits a reservation, the backend instantly broadcasts a lightweight update event to all active client browser tabs. The schedule grid updates and repaints visually on the fly (turning booked slots gray) without requiring the user to refresh their page.

### 4. Automated Integration Testing (xUnit Suite)
* **The Result:** The test suite bootstraps an in-memory web server via `WebApplicationFactory`, handles JWT bearer tokens seamlessly through dynamic claims generation, and fires parallel HTTP POST requests concurrently into the live endpoints to mathematically verify that the database concurrency protection remains 100% bulletproof under fire.

---

## 🛠️ System Version 1.0: Known Issues & Troubleshooting

As of Version 1.0, the core business engine, JWT-token validation, and live data streaming work stably. However, there is a minor client-side rendering anomaly regarding the visual representation of administrator-configured slots inside the regular customer hour grid.

### Possible Technical Cause:
  **JSON Property Case-Sensitivity (CamelCase Mismatch):**
   The decoupled `GET /api/customer/booking` endpoint serializes native transaction arrays. If the administrator's block records are returned with different JSON key configurations, the client-side JavaScript engine evaluates the token as `undefined`, failing to paint the hour button gray.

### 🔧 Roadmap for Version 1.1:
    ** solving known issues
    ** recoding admin interface and models to add  start and end dates to 
        limit customer access to book dates instead of booking date and times slots not chosen by administrator  
     ** limiting user  of choosing  past dates in user interface

## 💻 How to Run Automated Tests
To execute the automated integration test suite on your local machine, navigate to the project directory and run:
```bash
dotnet test
```
