import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './context/AuthContext'
import Login from './pages/Login'
import Register from './pages/Register'
import Tasks from './pages/Tasks'
import Progress from './pages/Progress'
import Review from './pages/Review'
import Units from './pages/Units'
import Users from './pages/Users'

function PrivateRoute({ children, managerOnly = false }) {
  const { token, role } = useAuth()
  if (!token) return <Navigate to="/login" />
  if (managerOnly && role !== 'Manager') return <Navigate to="/tasks" />
  return children
}

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/tasks" element={<PrivateRoute><Tasks /></PrivateRoute>} />
          <Route path="/progress" element={<PrivateRoute><Progress /></PrivateRoute>} />
          <Route path="/review" element={<PrivateRoute managerOnly><Review /></PrivateRoute>} />
          <Route path="/units" element={<PrivateRoute><Units /></PrivateRoute>} />
          <Route path="/users" element={<PrivateRoute><Users /></PrivateRoute>} />
          <Route path="*" element={<Navigate to="/login" />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
