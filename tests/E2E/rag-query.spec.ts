import { test, expect } from '@playwright/test';

test.describe('RAG Query', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/email|username/i).fill('test@example.com');
    await page.getByLabel(/password/i).fill('testpassword');
    await page.getByRole('button', { name: /login|sign in/i }).click();
    await expect(page).toHaveURL(/\/(papers|dashboard)?$/, { timeout: 10000 });
  });

  test('should import papers, query knowledge base, and verify answer', async ({ page }) => {
    await page.goto('/collections');

    const importButton = page.getByRole('button', { name: /import|add papers/i });
    if (await importButton.isVisible()) {
      await importButton.click();
    }

    const fileInput = page.locator('input[type="file"][accept*="pdf"]');
    await fileInput.setInputFiles(['test-data/paper1.pdf', 'test-data/paper2.pdf']);

    await expect(page.getByText(/importing|processing/i)).toBeVisible({ timeout: 10000 });

    await page.goto('/chat');

    const queryInput = page.getByPlaceholder(/search|query|ask/i);
    await expect(queryInput).toBeVisible();

    await queryInput.fill('What are the main findings from the imported papers?');
    await page.getByRole('button', { name: /send|search|ask/i }).click();

    await expect(page.getByText(/answer|response|result/i)).toBeVisible({ timeout: 30000 });
    const responseArea = page.locator('[data-testid="rag-response"], .response, [role="region"]').first();
    await expect(responseArea).not.toBeEmpty();
  });

  test('should handle empty knowledge base gracefully', async ({ page }) => {
    await page.goto('/chat');

    const queryInput = page.getByPlaceholder(/search|query|ask/i);
    await queryInput.fill('What are the key contributions?');
    await page.getByRole('button', { name: /send|search/i }).click();

    await expect(page.getByText(/no papers imported|empty knowledge base/i)).toBeVisible({ timeout: 15000 });
  });
});