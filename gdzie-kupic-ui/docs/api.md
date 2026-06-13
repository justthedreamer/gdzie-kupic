# Gdzie Kupić API

**Get product categories**

```http
GET: /api/categories
```

**Get user active posts**

```http
GET: /api/user/post/active
```

**Get user history posts**

```http
GET: /api/user/post/history
```

**Get merchant feed**

```http
GET: /api/merchant/feed
```

**Get merchant active responses**

```http
GET: /api/merchant/responses/active
```

**Get merchant history responses**

```http
GET: /api/merchant/responses/history
```

**Get merchant unaswered responses**

```http
GET: /api/merchant/responses/unanswered
```

---

**Register Merchant**

```http
POST: /api/merchant/register
{
    name: "Merchant",
    location: {
        hasMultiLocation: true
        locations: [""]
    },
    category-of-interest: []
}
```
