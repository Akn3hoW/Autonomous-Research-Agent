import { test, expect } from '@playwright/test';

test.describe('Paper Import', () => {
  test('should import a PDF and verify it appears in the paper list', async ({ page }) => {
    await page.goto('/papers');

    const importButton = page.getByRole('button', { name: /import|pdf/i });
    await expect(importButton).toBeVisible();

    const fileInput = page.locator('input[type="file"][accept*="pdf"]');
    await fileInput.setInputFiles('test-data/sample-paper.pdf');

    await expect(page.getByText(/importing|processing/i)).toBeVisible({ timeout: 10000 });

    await expect(page.getByText(/sample-paper\.pdf| uploaded successfully/i)).toBeVisible({ timeout: 30000 });
  });

  test('should show error for invalid file type', async ({ page }) => {
    await page.goto('/papers');

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles('test-data/invalid-file.txt');

    await expect(page.getByText(/invalid file type|unsupported format/i)).toBeVisible({ timeout: 5000 });
  });
});