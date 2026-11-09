const maxBytes = 5 * 1024 * 1024;
export const photoTypes = 'image/jpeg,image/png,image/webp,image/heic,image/heif,.heic,.heif';

export async function prepareUpload(file: File): Promise<File> {
  const standard = ['image/jpeg', 'image/png', 'image/webp'].includes(file.type);
  if (standard && file.size <= maxBytes) return file;
  if (file.size > 25 * 1024 * 1024) throw new Error('Hãy chọn ảnh nhỏ hơn 25 MB.');
  const image = new Image();
  const url = URL.createObjectURL(file);
  try {
    image.src = url;
    await image.decode();
    const scale = Math.min(1, 2048 / Math.max(image.naturalWidth, image.naturalHeight));
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
    canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
    const context = canvas.getContext('2d');
    if (!context) throw new Error();
    context.fillStyle = '#fff';
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.85));
    if (!blob || blob.size > maxBytes) throw new Error();
    return new File([blob], 'photo.jpg', { type: 'image/jpeg' });
  } catch {
    throw new Error('Điện thoại chưa chuyển được ảnh này. Hãy chọn ảnh JPEG, PNG hoặc WebP tối đa 5 MB.');
  } finally {
    URL.revokeObjectURL(url);
  }
}
