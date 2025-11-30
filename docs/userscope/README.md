# UserScope Documentation

Hierarchical data access control system for ShikshaMitra.

## Files

- **[UserScope.md](./UserScope.md)** - Complete architecture, design patterns, and implementation guide
- **[permissions-setup.md](./permissions-setup.md)** - Permission configuration and setup
- **[exception-handling.md](./exception-handling.md)** - Error handling patterns

## Quick Overview

### Concept
UserScope ensures users only access data within their assigned scope.

### Hierarchy
```
Board
  └── Medium
      └── Stage
          └── Standard (Class)
              └── Division
```

### Key Principle
**`NULL = ALL`** - If a level is NULL, user has access to ALL at that level.

### Example Scopes
- **Division Level**: Teacher assigned to "Class 5, Division A"
- **Class Level**: Teacher assigned to "Class 5" (all divisions)
- **Board Level**: Principal assigned to "CBSE Board" (all under CBSE)

## Start Here

1. **New to UserScope?** → Read [UserScope.md](./UserScope.md)
2. **Setting up permissions?** → See [permissions-setup.md](./permissions-setup.md)
3. **Handling errors?** → Check [exception-handling.md](./exception-handling.md)

---

[Back to main docs](../README.md)
