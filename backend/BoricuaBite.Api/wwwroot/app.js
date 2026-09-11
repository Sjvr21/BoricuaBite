let accessToken = null;
let refreshToken = null;
let selectedRestaurantId = null;

const byId = id => document.getElementById(id);
const log = (label, data) => {
  byId('log').textContent = `${label}\n${typeof data === 'string' ? data : JSON.stringify(data, null, 2)}`;
};

async function readResponse(response) {
  const text = await response.text();
  if (!text) return null;
  try { return JSON.parse(text); } catch { return text; }
}

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.body) headers['Content-Type'] = 'application/json';
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  let response = await fetch(path, { ...options, headers });

  if (response.status === 401 && refreshToken) {
    const refreshResponse = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });

    if (refreshResponse.ok) {
      const tokens = await refreshResponse.json();
      accessToken = tokens.accessToken;
      refreshToken = tokens.refreshToken;
      headers.Authorization = `Bearer ${accessToken}`;
      response = await fetch(path, { ...options, headers });
    }
  }

  const data = await readResponse(response);
  log(`${options.method || 'GET'} ${path} -> ${response.status}`, data);

  if (!response.ok) throw new Error(JSON.stringify(data));
  return data;
}

function restaurantPayload() {
  return {
    name: byId('rName').value,
    description: byId('rDescription').value,
    phoneNumber: byId('rPhone').value,
    address: {
      addressLine1: byId('rAddress').value,
      addressLine2: null,
      city: byId('rCity').value,
      stateOrTerritory: byId('rState').value,
      postalCode: byId('rPostal').value,
      country: byId('rCountry').value
    }
  };
}

function renderRestaurants(items) {
  const root = byId('restaurants');
  root.innerHTML = '';

  for (const restaurant of items) {
    const card = document.createElement('div');
    card.className = 'card';

    const title = document.createElement('strong');
    title.textContent = `${restaurant.name} - ${restaurant.isOpen ? 'OPEN' : 'CLOSED'}`;
    card.appendChild(title);

    const details = document.createElement('p');
    details.textContent = `${restaurant.address.city}, ${restaurant.address.stateOrTerritory}`;
    card.appendChild(details);

    const select = document.createElement('button');
    select.textContent = 'Select';
    select.addEventListener('click', () => {
      selectedRestaurantId = restaurant.id;
      byId('selectedId').textContent = restaurant.id;
      loadMenu();
    });
    card.appendChild(select);

    const availability = document.createElement('button');
    availability.textContent = restaurant.isOpen ? 'Close' : 'Open';
    availability.style.marginLeft = '8px';
    availability.addEventListener('click', async () => {
      await api(`/api/owner/restaurants/${restaurant.id}/availability`, {
        method: 'PUT',
        body: JSON.stringify({ isOpen: !restaurant.isOpen })
      });
      await loadRestaurants();
      await browse();
    });
    card.appendChild(availability);

    root.appendChild(card);
  }
}

function renderMenu(items) {
  const root = byId('menu');
  root.innerHTML = '';

  for (const item of items) {
    const card = document.createElement('div');
    card.className = 'card';

    const text = document.createElement('span');
    text.textContent = `${item.name} - $${Number(item.price).toFixed(2)} - ${item.isAvailable ? 'Available' : 'Sold out'}`;
    card.appendChild(text);

    const toggle = document.createElement('button');
    toggle.textContent = item.isAvailable ? 'Mark sold out' : 'Mark available';
    toggle.style.marginLeft = '8px';
    toggle.addEventListener('click', async () => {
      await api(`/api/owner/restaurants/${selectedRestaurantId}/menu-items/${item.id}/availability`, {
        method: 'PUT',
        body: JSON.stringify({ isAvailable: !item.isAvailable })
      });
      await loadMenu();
    });
    card.appendChild(toggle);

    const remove = document.createElement('button');
    remove.textContent = 'Delete';
    remove.style.marginLeft = '8px';
    remove.addEventListener('click', async () => {
      await api(`/api/owner/restaurants/${selectedRestaurantId}/menu-items/${item.id}`, { method: 'DELETE' });
      await loadMenu();
    });
    card.appendChild(remove);

    root.appendChild(card);
  }
}

async function register() {
  try {
    await api('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email: byId('email').value, password: byId('password').value })
    });
    byId('authStatus').textContent = 'Registered. Now log in.';
  } catch (error) {
    byId('authStatus').textContent = error.message;
  }
}

async function login() {
  try {
    const tokens = await api('/api/auth/login?useCookies=false', {
      method: 'POST',
      body: JSON.stringify({ email: byId('email').value, password: byId('password').value })
    });
    accessToken = tokens.accessToken;
    refreshToken = tokens.refreshToken;
    byId('authStatus').textContent = 'Logged in.';
    await account();
    await loadRestaurants();
  } catch (error) {
    byId('authStatus').textContent = error.message;
  }
}

async function account() {
  try {
    const result = await api('/api/account');
    byId('authStatus').textContent = `Signed in as ${result.email} (${result.id})`;
  } catch (error) {
    byId('authStatus').textContent = error.message;
  }
}

async function createRestaurant() {
  await api('/api/owner/restaurants', {
    method: 'POST',
    body: JSON.stringify(restaurantPayload())
  });
  await loadRestaurants();
}

async function loadRestaurants() {
  const restaurants = await api('/api/owner/restaurants');
  renderRestaurants(restaurants);
}

async function addItem() {
  if (!selectedRestaurantId) return alert('Select a restaurant first.');

  await api(`/api/owner/restaurants/${selectedRestaurantId}/menu-items`, {
    method: 'POST',
    body: JSON.stringify({
      name: byId('itemName').value,
      price: Number(byId('itemPrice').value)
    })
  });
  await loadMenu();
}

async function loadMenu() {
  if (!selectedRestaurantId) return;
  const page = await api(`/api/owner/restaurants/${selectedRestaurantId}/menu-items?page=1&pageSize=100`);
  renderMenu(page.items);
}

async function browse() {
  const query = new URLSearchParams({ page: '1', pageSize: '100' });
  if (byId('search').value) query.set('search', byId('search').value);
  if (byId('city').value) query.set('city', byId('city').value);

  const page = await api(`/api/restaurants?${query}`);
  const root = byId('catalog');
  root.innerHTML = '';

  for (const restaurant of page.items) {
    const card = document.createElement('div');
    card.className = 'card';

    const title = document.createElement('strong');
    title.textContent = `${restaurant.name} - ${restaurant.isOpen ? 'OPEN' : 'CLOSED'}`;
    card.appendChild(title);

    const description = document.createElement('p');
    description.textContent = restaurant.description;
    card.appendChild(description);

    const menuButton = document.createElement('button');
    menuButton.textContent = 'View public menu';
    menuButton.addEventListener('click', async () => {
      const menuPage = await api(`/api/restaurants/${restaurant.id}/menu?page=1&pageSize=100`);
      const lines = menuPage.items.map(item => `${item.name} - $${Number(item.price).toFixed(2)}${item.isAvailable ? '' : ' (sold out)'}`);
      alert(lines.length ? lines.join('\n') : 'No menu items.');
    });
    card.appendChild(menuButton);

    root.appendChild(card);
  }
}

byId('registerBtn').addEventListener('click', register);
byId('loginBtn').addEventListener('click', login);
byId('accountBtn').addEventListener('click', account);
byId('createRestaurantBtn').addEventListener('click', createRestaurant);
byId('loadRestaurantsBtn').addEventListener('click', loadRestaurants);
byId('addItemBtn').addEventListener('click', addItem);
byId('loadMenuBtn').addEventListener('click', loadMenu);
byId('browseBtn').addEventListener('click', browse);

browse().catch(error => log('Initial public browse failed', error.message));
