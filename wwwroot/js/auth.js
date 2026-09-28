const API_URL = '/api/auth'; 
let isLoginMode = true;

const formTitle = document.getElementById('form-title');
const authForm = document.getElementById('auth-form');
const submitBtn = document.getElementById('submit-btn');
const toggleModeBtn = document.getElementById('toggle-mode-btn');
const adminBlock = document.getElementById('admin-block'); 

toggleModeBtn.addEventListener('click', () => {
    isLoginMode = !isLoginMode;
    
    formTitle.innerText = isLoginMode ? "Sign In" : "Sign Up";
    submitBtn.innerText = isLoginMode ? "Sign In" : "Sign Up";
    toggleModeBtn.innerText = isLoginMode ? "Don't have an account? Sign Up" : "Already have an account? Sign In";
    
    if (isLoginMode) {
        adminBlock.classList.add('d-none');
    } else {
        adminBlock.classList.remove('d-none'); 
    }
});

authForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const name = document.getElementById('name').value;
    const password = document.getElementById('password').value;
    const isAdmin = isLoginMode ? false : document.getElementById('isAdmin').checked;

    const endpoint = isLoginMode ? '/login' : '/register';
    const bodyData = { name, password, isAdmin };

    try {
        const response = await fetch(`${API_URL}${endpoint}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(bodyData)
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.message || 'Authentication failed');
        }

        if (isLoginMode) {
            localStorage.setItem('token', data.token);
            localStorage.setItem('isAdmin', data.isAdmin);
            localStorage.setItem('name', data.name);
            
            alert('Sign In successful! Redirecting to dashboard.');
            window.location.href = 'index.html'; 
        } else {
            alert('Registration successful! Please sign in.');
            toggleModeBtn.click(); 
        }

    } catch (error) {
        alert('Error: ' + error.message);
    }
});
