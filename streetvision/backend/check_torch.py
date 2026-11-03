
import torch

print("PyTorch version:", torch.__version__)
print("CUDA available:", torch.cuda.is_available())

if torch.cuda.is_available():
    print("GPU:", torch.cuda.get_device_name(0))
    print("PyTorch CUDA runtime:", torch.version.cuda)

    device = torch.device("cuda")
else:
    device = torch.device("cpu")

print("Selected device:", device)

# Run a small calculation on the selected device.
x = torch.randn(2048, 2048, device=device)
y = x @ x

print("Tensor device:", y.device)
print("Tensor shape:", tuple(y.shape))
print("PyTorch GPU test passed." if device.type == "cuda"
      else "PyTorch works, but GPU support is not active.")
