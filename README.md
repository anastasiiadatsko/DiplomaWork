Запуск локально
1. Клонуй репозиторій в попередньо створену папку
git clone https://github.com/anastasiiadatsko/DiplomaWork.git

2. Налаштуй підключення до бази даних
Відкрий HabitFlow.Web/appsettings.json і вкажи свої дані PostgreSQL:
json{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=habitflow;Username=postgres;Password=ТВІЙпароль"
  }
}

База даних створюється автоматично при першому запуску — вручну нічого створювати не потрібно.

3. Застосуй міграції
через Package Manager Console у Visual Studio:
Update-Database -Project HabitFlow.DAL -StartupProject HabitFlow.Web

4. Запусти
Або https, або F5 у Visual Studio

5. Відкрий у браузері
https://localhost:7060


Вебзастосунок доступний за посиланням:  
https://habitflow-csl9.onrender.com/
