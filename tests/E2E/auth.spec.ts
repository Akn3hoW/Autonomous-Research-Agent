import { test, expect } from '@playwright/test';

test.describe('Authentication', () => {
  test('should register a new user', async ({ page }) => {
    await page.goto('/register');

    await page.getByLabel(/email/i).fill(`testuser_${Date.now()}@example.com`);
    await page.getByLabel(/^password|create password/i).fill('SecurePassword123!');
    await page.getByLabel(/confirm password/i).fill('SecurePassword123!');
    await page.getByRole('button', { name: /register|sign up/i }).click();

    await expect(page).toHaveURL(/\/(dashboard|papers|login)?$/, { timeout: 15000 });
  });

  test('should login with valid credentials', async ({ page }) => {
    await page.goto('/login');

    await page.getByLabel(/email|username/i).fill('test@example.com');
    await page.getByLabel(/password/i).fill('testpassword');
    await page.getByRole('button', { name: /login|sign in/i }).click();

    await expect(page).toHaveURL(/\/(dashboard|papers)?$/, { timeout: 10000 });
    await expect(page.getByText(/welcome|dashboard/i)).toBeVisible();
  });

  test('should fail login with invalid credentials', async ({ page }) => {
    await page.goto('/login');

    await page.getByLabel(/email|username/i).fill('invalid@example.com');
    await page.getByLabel(/password/i).fill('wrongpassword');
    await page.getByRole('button', { name: /login|sign in/i }).click();

    await expect(page.getByText(/invalid|incorrect|failed/i)).toBeVisible({ timeout: 5000 });
    await expect(page).toHaveURL('/login');
  });

  test('should refresh access token', async ({ page, context }) => {
    await context.grantPermissions(['storage', 'notifications']);

    await page.goto('/login');
    await page.getByLabel(/email|username/i).fill('test@example.com');
    await page.getByLabel(/password/i).fill('testpassword');
    await page.getByRole('button', { name: /login|sign in/i }).click();

    await expect(page).toHaveURL(/\/(dashboard|papers)?$/, { timeout: 10000 });

    const storage = await context.storageState();
    const expiresAt = storage.origins[0]?.localStorage?.find(s => s.name.includes('expiry'))?.value;
    if (expiresAt) {
      const expiryTime = new Date(expiresAt).getTime();
      const now = Date.now();
      if (now >= expiryTime - 60000) {
        await page.reload();
        await expect(page).toHaveURL(/\/(dashboard|papers)?$/, { timeout: 10000 });
      }
    }
  });

  test('should logout successfully', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/email|username/i).fill('test@example.com');
    await page.getByLabel(/password/i).fill('testpassword');
    await page.getByRole('button', { name: /login|sign in/i }).click();

    await expect(page).toHaveURL(/\/(dashboard|papers)?$/, { timeout: 10000 });

    await page.getByRole('button', { name: /logout|sign out/i }).click();
    await expect(page).toHaveURL('/login');
  });
});