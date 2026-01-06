@echo off
echo 🔄 Re-seeding POS database with comprehensive test data...
echo.

cd ReseedTool
dotnet run
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Database seeding failed!
    pause
    exit /b 1
)

echo.
echo 📋 Copying seeded database to application locations...
copy "%APPDATA%\OfflinePOS\pos_seed.db" "..\src\Desktop\pos.db" >nul
copy "%APPDATA%\OfflinePOS\pos_seed.db" "..\src\Mobile\pos.db" >nul

echo ✅ Database seeding completed successfully!
echo.
echo 🔐 Login Credentials:
echo    Administrator: admin / admin123
echo    Shop Manager:  manager / manager123
echo    Cashier:       cashier / cashier123
echo.
echo 🎉 Ready for testing! You can now run the POS applications.
echo.
pause